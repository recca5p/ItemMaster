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
        if (Log.Logger == Serilog.Core.Logger.None)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", "ItemMasterLambda")
                .WriteTo.Console(new RenderedCompactJsonFormatter())
                .CreateLogger();
        }
        Log.Information("Lambda init starting...");
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
            Log.Information("Get env config");

            var envRaw = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
            var envLower = envRaw.ToLowerInvariant();
            var basePath = Environment.GetEnvironmentVariable("CONFIG_BASE") ?? "/im";
            var ssmPath = $"{basePath}/{envLower}/";
            var regionName = Environment.GetEnvironmentVariable("AWS_REGION") ?? "ap-southeast-1";
            var awsOptions = new AWSOptions { Region = RegionEndpoint.GetBySystemName(regionName) };

            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{envRaw}.json", optional: true)
                .AddEnvironmentVariables() // still allows only the retained vars
                .AddSystemsManager(src =>
                {
                    src.Path = ssmPath;
                    src.AwsOptions = awsOptions;
                    src.Optional = true;
                    src.ReloadAfter = TimeSpan.FromMinutes(5);
                });
            Log.Information("Ssm: {ssmPath}", ssmPath);

            _configuration = builder.Build();
        }
        catch (Exception ex)
        {
            _startupError = true;
            _startupErrorMessage = "config_build_failure";
            ConfigureSerilog(null);
            Log.Error(ex, "ConfigBuildFailure");
        }
        Log.Information("start log cinfig");

        ConfigureSerilog(_configuration);

        if (!_startupError && _configuration is not null)
        {
            try
            {
                var envRawNow = _configuration["DOTNET_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
                var basePathNow = Environment.GetEnvironmentVariable("CONFIG_BASE") ?? "/im";
                var envLowerNow = envRawNow.ToLowerInvariant();
                var ssmPathNow = $"{basePathNow}/{envLowerNow}/";
                var regionNameNow = Environment.GetEnvironmentVariable("AWS_REGION") ?? "ap-southeast-1";
                Log.Information("ConfigInit env={Env} ssm_path={SsmPath} region={Region} base_path={BasePath}", envRawNow, ssmPathNow, regionNameNow, basePathNow);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "ConfigInitLogFailure");
            }
        }

        Log.Information("start service collection");

        var servicesFull = new ServiceCollection();
        servicesFull.AddSingleton<IClock, SystemClock>();
        servicesFull.AddSingleton<IConfigProvider, EnvConfigProvider>();
        if (_configuration != null)
            servicesFull.AddSingleton(_configuration);
        servicesFull.AddLogging(b => { b.ClearProviders(); b.AddSerilog(); });
        servicesFull.AddSingleton<IAmazonSecretsManager>(_ => new AmazonSecretsManagerClient());
        servicesFull.AddSingleton<IConnectionStringProvider, SecretsAwareMySqlConnectionStringProvider>();
        servicesFull.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient());

        // SQS config strictly from configuration (Parameter Store / JSON)
        Log.Information("Start fetch sqsUrl");
        string? sqsUrl = GetConfigValue("sqs:url");
        Log.Information("ConfigFetch key=sqs:url present={Present}", !string.IsNullOrWhiteSpace(sqsUrl));

        var maxRetriesRaw = GetConfigValue("sqs:max_retries");
        Log.Information("ConfigFetch key=sqs:max_retries raw={Raw}", maxRetriesRaw);
        var baseDelayPrimaryRaw = GetConfigValue("sqs:base_delay_ms");
        var baseDelayTypoRaw = GetConfigValue("sqs:base_deplay_ms");
        Log.Information("ConfigFetch key=sqs:base_delay_ms raw={Primary} key_typo=sqs:base_deplay_ms raw={Typo}", baseDelayPrimaryRaw, baseDelayTypoRaw);
        var backoffPrimaryRaw = GetConfigValue("sqs:backoff_multiplier");
        var backoffTypoRaw = GetConfigValue("sqs:backoff_multilier");
        Log.Information("ConfigFetch key=sqs:backoff_multiplier raw={Primary} key_typo=sqs:backoff_multilier raw={Typo}", backoffPrimaryRaw, backoffTypoRaw);
        var batchSizeRaw = GetConfigValue("sqs:batch_size");
        Log.Information("ConfigFetch key=sqs:batch_size raw={Raw}", batchSizeRaw);

        if (string.IsNullOrWhiteSpace(sqsUrl))
        {
            _startupError = true;
            _startupErrorMessage = "missing_sqs_url";
            Log.Error("StartupValidationFailure reason=missing_sqs_url");
        }

        static int ParseInt(string? raw, int def, Func<int, bool>? predicate = null)
            => (int.TryParse(raw, out var v) && (predicate == null || predicate(v))) ? v : def;
        static double ParseDouble(string? raw, double def, Func<double, bool>? predicate = null)
            => (double.TryParse(raw, out var v) && (predicate == null || predicate(v))) ? v : def;

        int maxRetries = ParseInt(maxRetriesRaw, 2, v => v >= 0 && v <= 10);
        var baseDelayRaw = baseDelayPrimaryRaw ?? baseDelayTypoRaw;
        int baseDelayMs = ParseInt(baseDelayRaw, 1000, v => v > 0);
        var backoffRaw = backoffPrimaryRaw ?? backoffTypoRaw;
        double backoffMultiplier = ParseDouble(backoffRaw, 2.0, v => v > 1.0);
        int batchSize = ParseInt(batchSizeRaw, 100, v => v > 0 && v <= 500);

        Log.Information("ConfigParsed sqs:url_present={UrlPresent} max_retries={MaxRetries} base_delay_ms={BaseDelay} backoff_multiplier={Backoff} batch_size={BatchSize}", !string.IsNullOrWhiteSpace(sqsUrl), maxRetries, baseDelayMs, backoffMultiplier, batchSize);

        // MySQL diagnostic (do not attempt secret fetch here, only log presence of config keys)
        var mysqlHost = GetConfigValue("mysql:host");
        var mysqlDb = GetConfigValue("mysql:db");
        var mysqlPort = GetConfigValue("mysql:port");
        var mysqlSsl = GetConfigValue("mysql:ssl_mode");
        var mysqlSecretArn = GetConfigValue("mysql:secret_arn");
        Log.Information("ConfigFetch mysql: host_present={HostPresent} db_present={DbPresent} port_raw={Port} ssl_mode_raw={Ssl} secret_arn_present={SecretPresent}", !string.IsNullOrWhiteSpace(mysqlHost), !string.IsNullOrWhiteSpace(mysqlDb), mysqlPort, mysqlSsl, !string.IsNullOrWhiteSpace(mysqlSecretArn));

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

        if (!_startupError)
            servicesFull.AddScoped<IProcessSkusUseCase, ProcessSkusUseCase>();

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
        var levelRaw = configuration?["log_level"] ?? "Information"; // no env fallback anymore
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
