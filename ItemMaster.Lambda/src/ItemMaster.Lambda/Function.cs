using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using ItemMaster.Contracts;
using ItemMaster.Application;
using ItemMaster.Shared;
using ItemMaster.Infrastructure;
using ItemMaster.Infrastructure.Ef;
using ItemMaster.Infrastructure.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Amazon.SecretsManager;
using Pomelo.EntityFrameworkCore.MySql.Storage;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Context;
using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Amazon.Extensions.Configuration.SystemsManager;
using Amazon.Extensions.NETCore.Setup;
using Amazon;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ItemMaster.Lambda;

public class Function
{
    private static readonly ServiceProvider ServiceProvider;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static bool _migrationsApplied;
    private static bool _startupError;
    private static string? _startupErrorMessage;
    private static IConfiguration? _configuration;
    private static string? GetConfigValue(string key) => _configuration?[key];

    static Function()
    {
        var testMode = Environment.GetEnvironmentVariable("ITEMMASTER_TEST_MODE")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        if (testMode)
        {
            ConfigureSerilog(null);
            var services = new ServiceCollection();
            services.AddSingleton<IClock, SystemClock>();
            services.AddSingleton<IConfigProvider, EnvConfigProvider>();
            services.AddLogging(b => { b.ClearProviders(); b.AddSerilog(); });
            services.AddDbContext<ItemMasterDbContext>(o => o.UseInMemoryDatabase("ItemMasterTest"));
            services.AddScoped<IItemMasterLogRepository, MySqlItemMasterLogRepository>();
            services.AddScoped<IItemPublisher, InMemoryItemPublisher>();
            services.AddScoped<IProcessSkusUseCase, ProcessSkusUseCase>();
            ServiceProvider = services.BuildServiceProvider();
            return;
        }

        try
        {
            var envRaw = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
            var envLower = envRaw.ToLowerInvariant();
            var basePath = Environment.GetEnvironmentVariable("CONFIG_BASE") ?? "/im";
            var regionName = Environment.GetEnvironmentVariable("AWS_REGION") ?? "ap-southeast-1";
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{envRaw}.json", optional: true)
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
        servicesFull.AddLogging(b => { b.ClearProviders(); b.AddSerilog(); });
        servicesFull.AddSingleton<IAmazonSecretsManager>(_ => new AmazonSecretsManagerClient());
        servicesFull.AddSingleton<IConnectionStringProvider, SecretsAwareMySqlConnectionStringProvider>();
        servicesFull.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient());

        string? sqsUrl = GetConfigValue("sqs:url");
        if (string.IsNullOrWhiteSpace(sqsUrl))
        {
            _startupError = true;
            _startupErrorMessage = "missing_sqs_url";
            Log.Error("StartupValidationFailure reason=missing_sqs_url");
        }

        static int ParseInt(string? raw, int def, Func<int, bool>? predicate = null) => (int.TryParse(raw, out var v) && (predicate == null || predicate(v))) ? v : def;
        static double ParseDouble(string? raw, double def, Func<double, bool>? predicate = null) => (double.TryParse(raw, out var v) && (predicate == null || predicate(v))) ? v : def;

        var maxRetriesRaw = GetConfigValue("sqs:max_retries");
        var baseDelayRaw = GetConfigValue("sqs:base_delay_ms") ?? GetConfigValue("sqs:base_deplay_ms");
        var backoffRaw = GetConfigValue("sqs:backoff_multiplier") ?? GetConfigValue("sqs:backoff_multilier");
        var batchSizeRaw = GetConfigValue("sqs:batch_size");

        int maxRetries = ParseInt(maxRetriesRaw, 2, v => v >= 0 && v <= 10);
        int baseDelayMs = ParseInt(baseDelayRaw, 1000, v => v > 0);
        double backoffMultiplier = ParseDouble(backoffRaw, 2.0, v => v > 1.0);
        int batchSize = ParseInt(batchSizeRaw, 100, v => v > 0 && v <= 500);

        var mysqlHost = GetConfigValue("mysql:host");
        var mysqlDb = GetConfigValue("mysql:db");
        var mysqlSecretArn = GetConfigValue("mysql:secret_arn");
        Log.Information("StartupConfig sqs_url_present={Sqs} max_retries={MaxRetries} base_delay_ms={BaseDelay} backoff={Backoff} batch_size={Batch} mysql_host={HasHost} mysql_db={HasDb} mysql_secret={HasSecret}", !string.IsNullOrWhiteSpace(sqsUrl), maxRetries, baseDelayMs, backoffMultiplier, batchSize, !string.IsNullOrWhiteSpace(mysqlHost), !string.IsNullOrWhiteSpace(mysqlDb), !string.IsNullOrWhiteSpace(mysqlSecretArn));

        if (!_startupError)
        {
            servicesFull.AddSingleton(new SqsItemPublisherOptions
            {
                QueueUrl = sqsUrl!,
                MaxRetries = maxRetries,
                BaseDelayMs = baseDelayMs,
                BackoffMultiplier = backoffMultiplier,
                BatchSize = batchSize
            });
            servicesFull.AddScoped<IItemPublisher, SqsItemPublisher>();
        }

        string? connStr = null;
        if (!_startupError)
        {
            try
            {
                using var temp = servicesFull.BuildServiceProvider();
                connStr = temp.GetRequiredService<IConnectionStringProvider>().GetMySqlConnectionString();
            }
            catch (Exception ex)
            {
                _startupError = true;
                _startupErrorMessage = "conn_resolution";
                Log.Error(ex, "ConnResolutionFailure");
            }
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
                    servicesFull.AddDbContext<ItemMasterDbContext>(o => o.UseMySql(connStr, serverVersion));
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

        if (!_startupError) servicesFull.AddScoped<IProcessSkusUseCase, ProcessSkusUseCase>();

        ServiceProvider = servicesFull.BuildServiceProvider();

        if (!_startupError)
        {
            var apply = Environment.GetEnvironmentVariable("APPLY_MIGRATIONS") ?? Environment.GetEnvironmentVariable("APPLY_MIGATIONS");
            if (string.Equals(apply, "true", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var scope = ServiceProvider.CreateScope();
                    var ctx = scope.ServiceProvider.GetService<ItemMasterDbContext>();
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
    }

    private static void ConfigureSerilog(IConfiguration? configuration)
    {
        if (Log.Logger is Serilog.Core.Logger l && l != Serilog.Core.Logger.None) return;
        var levelRaw = configuration?["log_level"] ?? "Information";
        var parsed = levelRaw.ToLowerInvariant() switch
        {
            "debug" => Serilog.Events.LogEventLevel.Debug,
            "verbose" => Serilog.Events.LogEventLevel.Verbose,
            "warning" => Serilog.Events.LogEventLevel.Warning,
            "error" => Serilog.Events.LogEventLevel.Error,
            "fatal" => Serilog.Events.LogEventLevel.Fatal,
            _ => Serilog.Events.LogEventLevel.Information
        };
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(parsed)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", "ItemMasterLambda")
            .WriteTo.Console(new RenderedCompactJsonFormatter())
            .CreateLogger();
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (_startupError)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Body = JsonSerializer.Serialize(new { error = _startupErrorMessage ?? "startup" }, JsonOptions),
                Headers = new Dictionary<string, string>{{"Content-Type","application/json"}}
            };
        }
        using (LogContext.PushProperty("AwsRequestId", context.AwsRequestId))
        {
            using var scope = ServiceProvider.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Function>>();
            ProcessSkusRequest? input = null;
            var bodyRaw = request.Body;
            if (!string.IsNullOrWhiteSpace(bodyRaw))
            {
                if (request.IsBase64Encoded)
                {
                    try
                    {
                        var bytes = Convert.FromBase64String(bodyRaw);
                        bodyRaw = System.Text.Encoding.UTF8.GetString(bytes);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Base64DecodeFailure");
                    }
                }
                try
                {
                    input = JsonSerializer.Deserialize<ProcessSkusRequest>(bodyRaw, JsonOptions);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "BodyDeserializationFailure");
                }
            }
            input ??= new ProcessSkusRequest();
            var requestId = context.AwsRequestId ?? Guid.NewGuid().ToString("N");
            using (LogContext.PushProperty("RequestId", requestId))
            {
                var useCase = scope.ServiceProvider.GetRequiredService<IProcessSkusUseCase>();
                try
                {
                    var response = await useCase.ExecuteAsync(input.Skus, "api", requestId, CancellationToken.None);
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = 200,
                        Body = JsonSerializer.Serialize(response, JsonOptions),
                        Headers = new Dictionary<string, string>{{"Content-Type","application/json"}}
                    };
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "UseCaseFailure");
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = 500,
                        Body = JsonSerializer.Serialize(new { error = "internal_error" }, JsonOptions),
                        Headers = new Dictionary<string, string>{{"Content-Type","application/json"}}
                    };
                }
            }
        }
    }
}
