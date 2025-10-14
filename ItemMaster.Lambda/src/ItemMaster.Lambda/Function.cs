using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.CloudWatch;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.SecretsManager;
using Amazon.SQS;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using ItemMaster.Application;
using ItemMaster.Contracts;
using ItemMaster.Infrastructure;
using ItemMaster.Infrastructure.Ef;
using ItemMaster.Infrastructure.Observability;
using ItemMaster.Infrastructure.Secrets;
using ItemMaster.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace ItemMaster.Lambda;

public class Function
{
    private static readonly ServiceProvider ServiceProvider;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly bool _migrationsApplied;
    private static readonly bool _startupError;
    private static readonly string? _startupErrorMessage;
    private static readonly IConfiguration? _configuration;

    static Function()
    {
        AWSSDKHandler.RegisterXRayForAllServices();

        var testMode = Environment.GetEnvironmentVariable("ITEMMASTER_TEST_MODE")
            ?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        if (testMode)
        {
            ConfigureSerilog(null);
            var services = new ServiceCollection();
            services.AddSingleton<IClock, SystemClock>();
            services.AddSingleton<IConfigProvider, EnvConfigProvider>();
            services.AddLogging(b =>
            {
                b.ClearProviders();
                b.AddSerilog();
            });
            services.AddDbContext<MySqlDbContext>(o => o.UseInMemoryDatabase("ItemMasterTest"));
            services.AddScoped<IItemMasterLogRepository, InMemoryItemMasterLogRepository>();
            services.AddScoped<IItemPublisher, InMemoryItemPublisher>();
            services.AddScoped<ISnowflakeRepository, InMemorySnowflakeRepository>();
            services.AddScoped<IProcessSkusUseCase, ProcessSkusUseCase>();
            services.AddScoped<IMetricsService, InMemoryMetricsService>();
            services.AddScoped<ITracingService, InMemoryTracingService>();
            services.AddScoped<IObservabilityService, ObservabilityService>();
            services.AddSingleton<IRequestSourceDetector, RequestSourceDetector>();
            ServiceProvider = services.BuildServiceProvider();
            return;
        }

        try
        {
            var envRaw = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                         throw new InvalidOperationException("DOTNET_ENVIRONMENT environment variable is required");
            var envLower = envRaw.ToLowerInvariant();
            var basePath = Environment.GetEnvironmentVariable("CONFIG_BASE") ??
                           throw new InvalidOperationException("CONFIG_BASE environment variable is required");
            var regionName = Environment.GetEnvironmentVariable("REGION") ??
                             throw new InvalidOperationException("REGION environment variable is required");
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true)
                .AddJsonFile($"appsettings.{envRaw}.json", true)
                .AddEnvironmentVariables()
                .AddSystemsManager(src =>
                {
                    src.Path = $"{basePath}/{envLower}/";
                    src.AwsOptions = new AWSOptions { Region = RegionEndpoint.GetBySystemName(regionName) };
                    src.Optional = true;
                    src.ReloadAfter = TimeSpan.FromMinutes(5);
                });
            _configuration = builder.Build();
        }
        catch (Exception ex)
        {
            _startupError = true;
            _startupErrorMessage = "config_build_failure";
            ConfigureSerilog(null);
            Log.Error(ex, "ConfigBuildFailure");
        }

        ConfigureSerilog(_configuration);

        var servicesFull = new ServiceCollection();
        servicesFull.AddSingleton<IClock, SystemClock>();
        servicesFull.AddSingleton<IConfigProvider, EnvConfigProvider>();
        if (_configuration != null) servicesFull.AddSingleton(_configuration);
        servicesFull.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddSerilog();
        });
        servicesFull.AddSingleton<IAmazonSecretsManager, AmazonSecretsManagerClient>();
        servicesFull.AddSingleton<IConnectionStringProvider, SecretsAwareMySqlConnectionStringProvider>();
        servicesFull.AddSingleton<IAmazonSQS, AmazonSQSClient>();
        servicesFull.AddSingleton<IAmazonCloudWatch, AmazonCloudWatchClient>();

        var sqsUrl = GetConfigValue("sqs:url");
        if (string.IsNullOrWhiteSpace(sqsUrl))
        {
            _startupError = true;
            _startupErrorMessage = "missing_sqs_url";
            Log.Error("StartupValidationFailure reason=missing_sqs_url");
        }

        static int ParseInt(string? raw, int def, Func<int, bool>? predicate = null)
        {
            return int.TryParse(raw, out var v) && (predicate == null || predicate(v)) ? v : def;
        }

        static double ParseDouble(string? raw, double def, Func<double, bool>? predicate = null)
        {
            return double.TryParse(raw, out var v) && (predicate == null || predicate(v)) ? v : def;
        }

        var maxRetriesRaw = GetConfigValue("sqs:max_retries");
        var baseDelayRaw = GetConfigValue("sqs:base_delay_ms");
        var backoffRaw = GetConfigValue("sqs:backoff_multiplier");
        var batchSizeRaw = GetConfigValue("sqs:batch_size");

        var circuitBreakerFailureThresholdRaw = GetConfigValue("sqs:circuit_breaker_failure_threshold");
        var circuitBreakerDurationOfBreakRaw = GetConfigValue("sqs:circuit_breaker_duration_of_break_seconds");
        var circuitBreakerSamplingDurationRaw = GetConfigValue("sqs:circuit_breaker_sampling_duration_seconds");
        var circuitBreakerMinimumThroughputRaw = GetConfigValue("sqs:circuit_breaker_minimum_throughput");

        var maxRetries = ParseInt(maxRetriesRaw, 2, v => v >= 0 && v <= 10);
        var baseDelayMs = ParseInt(baseDelayRaw, 1000, v => v > 0);
        var backoffMultiplier = ParseDouble(backoffRaw, 2.0, v => v > 1.0);
        var batchSize = ParseInt(batchSizeRaw, 100, v => v > 0 && v <= 500);

        var circuitBreakerFailureThreshold = ParseInt(circuitBreakerFailureThresholdRaw, 5, v => v > 0 && v <= 20);
        var circuitBreakerDurationOfBreakSeconds =
            ParseInt(circuitBreakerDurationOfBreakRaw, 30, v => v > 0 && v <= 300);
        var circuitBreakerSamplingDurationSeconds =
            ParseInt(circuitBreakerSamplingDurationRaw, 60, v => v > 0 && v <= 600);
        var circuitBreakerMinimumThroughput = ParseInt(circuitBreakerMinimumThroughputRaw, 3, v => v > 0 && v <= 100);

        if (!_startupError)
        {
            servicesFull.Configure<SqsItemPublisherOptions>(opts =>
            {
                opts.QueueUrl = sqsUrl!;
                opts.MaxRetries = maxRetries;
                opts.BaseDelayMs = baseDelayMs;
                opts.BackoffMultiplier = backoffMultiplier;
                opts.BatchSize = batchSize;

                opts.CircuitBreakerFailureThreshold = circuitBreakerFailureThreshold;
                opts.CircuitBreakerDurationOfBreak = TimeSpan.FromSeconds(circuitBreakerDurationOfBreakSeconds);
                opts.CircuitBreakerSamplingDuration = circuitBreakerSamplingDurationSeconds;
                opts.CircuitBreakerMinimumThroughput = circuitBreakerMinimumThroughput;
            });
            servicesFull.AddScoped<IItemPublisher, SqsItemPublisher>();
        }

        string? connStr = null;
        if (!_startupError)
            try
            {
                using var temp = servicesFull.BuildServiceProvider();
                var connProvider = temp.GetRequiredService<IConnectionStringProvider>();
                connStr = connProvider.GetMySqlConnectionStringAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _startupError = true;
                _startupErrorMessage = "conn_resolution";
                Log.Error(ex, "ConnResolutionFailure");
            }

        if (!_startupError)
        {
            if (string.IsNullOrWhiteSpace(connStr))
            {
                _startupError = true;
                _startupErrorMessage = "missing_connection_string";
            }
            else
            {
                try
                {
                    var serverVersion = ServerVersion.AutoDetect(connStr);
                    servicesFull.AddDbContext<MySqlDbContext>(o => o.UseMySql(connStr, serverVersion));
                    servicesFull.AddScoped<IItemMasterLogRepository, MySqlItemMasterLogRepository>();
                }
                catch (Exception ex)
                {
                    _startupError = true;
                    _startupErrorMessage = "db_context_config";
                    Log.Error(ex, "DbContextConfigFailure");
                }
            }
        }

        if (!_startupError)
        {
            servicesFull.AddScoped<SnowflakeConnectionProvider>();
            var sfDb = GetConfigValue("snowflake:database");
            var sfSchema = GetConfigValue("snowflake:schema");
            var sfTable = GetConfigValue("snowflake:table");
            var sfWarehouse = GetConfigValue("snowflake:warehouse");

            if (string.IsNullOrWhiteSpace(sfDb) || string.IsNullOrWhiteSpace(sfSchema) ||
                string.IsNullOrWhiteSpace(sfTable) || string.IsNullOrWhiteSpace(sfWarehouse))
            {
                _startupError = true;
                _startupErrorMessage = "missing_snowflake_config";
                Log.Error(
                    "StartupValidationFailure reason=missing_snowflake_config database={Database} schema={Schema} table={Table} warehouse={Warehouse}",
                    !string.IsNullOrWhiteSpace(sfDb), !string.IsNullOrWhiteSpace(sfSchema),
                    !string.IsNullOrWhiteSpace(sfTable), !string.IsNullOrWhiteSpace(sfWarehouse));
            }
            else
            {
                servicesFull.Configure<SnowflakeOptions>(opts =>
                {
                    opts.Database = sfDb!;
                    opts.Schema = sfSchema!;
                    opts.Table = sfTable!;
                });

                servicesFull.AddScoped<ISnowflakeItemQueryBuilder, SnowflakeItemQueryBuilder>();
                servicesFull.AddScoped<ISnowflakeRepository, SnowflakeRepository>();
            }

            servicesFull.AddScoped<IMetricsService, CloudWatchMetricsService>();
            servicesFull.AddScoped<ITracingService, XRayTracingService>();
            servicesFull.AddScoped<IObservabilityService, ObservabilityService>();
            servicesFull.AddSingleton<IRequestSourceDetector, RequestSourceDetector>();
        }

        if (!_startupError) servicesFull.AddScoped<IProcessSkusUseCase, ProcessSkusUseCase>();

        ServiceProvider = servicesFull.BuildServiceProvider();

        if (!_startupError)
        {
            var apply = Environment.GetEnvironmentVariable("APPLY_MIGRATIONS") ??
                        Environment.GetEnvironmentVariable("APPLY_MIGATIONS");
            if (string.Equals(apply, "true", StringComparison.OrdinalIgnoreCase))
                try
                {
                    using var scope = ServiceProvider.CreateScope();
                    var ctx = scope.ServiceProvider.GetService<MySqlDbContext>();
                    if (ctx != null && !_migrationsApplied)
                    {
                        ctx.Database.Migrate();
                        _migrationsApplied = true;
                    }
                }
                catch (Exception ex)
                {
                    _startupError = true;
                    _startupErrorMessage = "migration_failure";
                    Log.Error(ex, "MigrationFailure");
                }
        }
    }

    private static string? GetConfigValue(string key)
    {
        return _configuration?[key];
    }

    private static void ConfigureSerilog(IConfiguration? configuration)
    {
        if (Log.Logger is Logger l && l != Logger.None) return;
        var levelRaw = configuration?["log_level"] ?? "Information";
        var parsed = levelRaw.ToLowerInvariant() switch
        {
            "debug" => LogEventLevel.Debug,
            "verbose" => LogEventLevel.Verbose,
            "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(parsed)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", "ItemMasterLambda")
            .WriteTo.Console(new RenderedCompactJsonFormatter())
            .CreateLogger();
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(object input, ILambdaContext context)
    {
        if (_startupError)
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Body = JsonSerializer.Serialize(new { error = _startupErrorMessage ?? "startup" }, JsonOptions),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };

        using var scope = ServiceProvider.CreateScope();
        
        string inputJson;
        try
        {
            inputJson = input is string str ? str : JsonSerializer.Serialize(input, JsonOptions);
        }
        catch
        {
            inputJson = "{}";
        }

        var requestSource = DetectRequestSource(inputJson);
        
        if (requestSource == RequestSource.CicdHealthCheck)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = JsonSerializer.Serialize(new 
                { 
                    status = "healthy",
                    message = "Lambda function is operational",
                    timestamp = DateTime.UtcNow,
                    source = "health_check"
                }, JsonOptions),
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }

        var observabilityService = scope.ServiceProvider.GetRequiredService<IObservabilityService>();
        var traceId = observabilityService.GetCurrentTraceId();

        using (LogContext.PushProperty("AwsRequestId", context.AwsRequestId))
        using (LogContext.PushProperty("RequestSource", requestSource.ToString()))
        using (LogContext.PushProperty("TraceId", traceId))
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Function>>();

            return await observabilityService.ExecuteWithObservabilityAsync(
                "LambdaHandler",
                requestSource,
                async () =>
                {
                    logger.LogInformation("Processing request from source: {RequestSource} | TraceId: {TraceId}",
                        requestSource, traceId);

                    ProcessSkusRequest? processRequest = null;
                    
                    if (requestSource == RequestSource.EventBridge)
                    {
                        try
                        {
                            var eventDoc = JsonDocument.Parse(inputJson);
                            if (eventDoc.RootElement.TryGetProperty("detail", out var detail))
                            {
                                processRequest = JsonSerializer.Deserialize<ProcessSkusRequest>(
                                    detail.GetRawText(), JsonOptions);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to parse EventBridge detail, using default request");
                        }
                    }
                    else if (requestSource == RequestSource.ApiGateway)
                    {
                        // API Gateway request - parse body
                        try
                        {
                            var apiGwRequest = JsonSerializer.Deserialize<APIGatewayProxyRequest>(inputJson, JsonOptions);
                            var bodyRaw = apiGwRequest?.Body;
                            
                            if (!string.IsNullOrWhiteSpace(bodyRaw))
                            {
                                if (apiGwRequest?.IsBase64Encoded == true)
                                {
                                    try
                                    {
                                        var bytes = Convert.FromBase64String(bodyRaw);
                                        bodyRaw = Encoding.UTF8.GetString(bytes);
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.LogError(ex, "Base64DecodeFailure | TraceId: {TraceId}", traceId);
                                        return new APIGatewayProxyResponse
                                        {
                                            StatusCode = 400,
                                            Body = JsonSerializer.Serialize(new { error = "base64_decode_failure", traceId },
                                                JsonOptions),
                                            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                                        };
                                    }
                                }

                                processRequest = JsonSerializer.Deserialize<ProcessSkusRequest>(bodyRaw, JsonOptions);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "ApiGatewayRequestParseFailure | TraceId: {TraceId}", traceId);
                        }
                    }
                    else
                    {
                        try
                        {
                            processRequest = JsonSerializer.Deserialize<ProcessSkusRequest>(inputJson, JsonOptions);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "DirectInvocationParseFailure | TraceId: {TraceId}", traceId);
                        }
                    }

                    processRequest ??= new ProcessSkusRequest();

                    var useCase = scope.ServiceProvider.GetRequiredService<IProcessSkusUseCase>();
                    var result = await useCase.ExecuteAsync(processRequest, requestSource, traceId,
                        context.RemainingTime > TimeSpan.FromSeconds(30)
                            ? new CancellationTokenSource(context.RemainingTime.Subtract(TimeSpan.FromSeconds(30)))
                                .Token
                            : CancellationToken.None);

                    if (result.IsSuccess)
                    {
                        logger.LogInformation("Lambda execution completed successfully | TraceId: {TraceId}", traceId);
                        return new APIGatewayProxyResponse
                        {
                            StatusCode = 200,
                            Body = JsonSerializer.Serialize(new
                            {
                                success = true,
                                data = result.Value,
                                traceId
                            }, JsonOptions),
                            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                        };
                    }

                    logger.LogError("ProcessSkus failed: {Error} | TraceId: {TraceId}", result.ErrorMessage, traceId);
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = 500,
                        Body = JsonSerializer.Serialize(new { error = result.ErrorMessage, traceId }, JsonOptions),
                        Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
                    };
                },
                new Dictionary<string, object>
                {
                    ["awsRequestId"] = context.AwsRequestId,
                    ["functionName"] = context.FunctionName,
                    ["functionVersion"] = context.FunctionVersion,
                    ["memoryLimitInMB"] = context.MemoryLimitInMB,
                    ["remainingTimeMs"] = context.RemainingTime.TotalMilliseconds
                });
        }
    }

    private static RequestSource DetectRequestSource(string inputJson)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(inputJson) || 
                inputJson.Trim() == "{}" || 
                inputJson.Trim() == "null")
            {
                return RequestSource.CicdHealthCheck;
            }

            var doc = JsonDocument.Parse(inputJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("source", out var source))
            {
                var sourceStr = source.GetString()?.ToLowerInvariant() ?? "";
                if (sourceStr.StartsWith("aws.") || 
                    sourceStr.Contains("eventbridge") ||
                    root.TryGetProperty("detail-type", out _))
                {
                    return RequestSource.EventBridge;
                }
            }

            if (root.TryGetProperty("requestContext", out var requestContext) &&
                requestContext.TryGetProperty("requestId", out _) &&
                requestContext.TryGetProperty("stage", out _))
            {
                return RequestSource.ApiGateway;
            }

            return RequestSource.Lambda;
        }
        catch
        {
            return RequestSource.Lambda;
        }
    }
}