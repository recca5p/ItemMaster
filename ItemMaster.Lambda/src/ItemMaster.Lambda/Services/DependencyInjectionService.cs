using System.Diagnostics.CodeAnalysis;
using Amazon.CloudWatch;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SQS;
using ItemMaster.Application;
using ItemMaster.Application.Services;
using ItemMaster.Infrastructure;
using ItemMaster.Infrastructure.Ef;
using ItemMaster.Infrastructure.Observability;
using ItemMaster.Infrastructure.Secrets;
using ItemMaster.Lambda.Configuration;
using ItemMaster.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ItemMaster.Lambda.Services;

public interface IDependencyInjectionService
{
    ServiceProvider BuildServiceProvider(IConfiguration configuration, bool isTestMode);
}

[ExcludeFromCodeCoverage]
public class DependencyInjectionService : IDependencyInjectionService
{
    private readonly IConfigurationValidationService _configValidationService;
    private readonly IConnectionStringService _connectionStringService;

    public DependencyInjectionService(
        IConfigurationValidationService configValidationService,
        IConnectionStringService connectionStringService)
    {
        _configValidationService = configValidationService;
        _connectionStringService = connectionStringService;
    }

    public ServiceProvider BuildServiceProvider(IConfiguration configuration, bool isTestMode)
    {
        var services = new ServiceCollection();

        RegisterCoreServices(services, configuration);

        RegisterLambdaServices(services);

        if (isTestMode)
            RegisterTestModeServices(services, configuration);
        else
            RegisterProductionServices(services, configuration);

        return services.BuildServiceProvider();
    }

    private void RegisterCoreServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IConfigProvider, EnvConfigProvider>();
        services.AddSingleton(configuration);
        services.AddMemoryCache();
        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddSerilog();
        });

        RegisterConfigurationOptions(services, configuration);
    }

    private static void RegisterConfigurationOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CacheConfiguration>(configuration.GetSection(CacheConfiguration.SECTION_NAME));
        services.Configure<DatabaseConfiguration>(configuration.GetSection(DatabaseConfiguration.SECTION_NAME));
        services.Configure<ObservabilityConfiguration>(
            configuration.GetSection(ObservabilityConfiguration.SECTION_NAME));
        services.Configure<ProcessingConfiguration>(configuration.GetSection(ProcessingConfiguration.SECTION_NAME));
        services.Configure<SqsItemPublisherOptions>(configuration.GetSection("SQS"));
        services.Configure<SnowflakeOptions>(configuration.GetSection("Snowflake"));
    }

    private void RegisterLambdaServices(IServiceCollection services)
    {
        services.AddScoped<IRequestProcessingService, RequestProcessingService>();
        services.AddScoped<IResponseService, ResponseService>();
        services.AddScoped<ICachedSecretService, CachedSecretService>();
        services.AddScoped<IDatabaseMigrationService, DatabaseMigrationService>();
    }

    private void RegisterTestModeServices(IServiceCollection services, IConfiguration configuration)
    {
        var mysqlHost = Environment.GetEnvironmentVariable("MYSQL_HOST");
        var isIntegrationTestMode = !string.IsNullOrEmpty(mysqlHost);

        Console.WriteLine(
            $"[DI] Registering test mode services. Integration test mode: {isIntegrationTestMode}, MYSQL_HOST: {mysqlHost ?? "not set"}, AWS_ENDPOINT_URL: {Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL") ?? "not set"}, SQS_URL: {configuration[ConfigurationConstants.SQS_URL] ?? "not set"}");

        if (isIntegrationTestMode)
        {
            var mysqlDb = Environment.GetEnvironmentVariable("MYSQL_DATABASE") ?? "item_master";
            var mysqlUser = Environment.GetEnvironmentVariable("MYSQL_USER") ?? "im_user";
            var mysqlPass = Environment.GetEnvironmentVariable("MYSQL_PASSWORD") ?? "im_pass";
            var connectionString =
                $"Server={mysqlHost};Database={mysqlDb};User={mysqlUser};Password={mysqlPass};CharSet=utf8mb4;";

            services.AddDbContext<MySqlDbContext>(options =>
            {
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            });

            // Use LocalStack-aware AWS clients for integration tests
            var awsEndpoint = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
            if (!string.IsNullOrEmpty(awsEndpoint))
            {
                // For LocalStack, use explicit credentials and don't set RegionEndpoint
                // Setting RegionEndpoint with ServiceURL can cause credential validation issues
                var credentials = new BasicAWSCredentials("test", "test");

                services.AddSingleton<IAmazonSecretsManager>(sp =>
                {
                    var config = new AmazonSecretsManagerConfig
                    {
                        ServiceURL = awsEndpoint,
                        UseHttp = true
                    };
                    return new AmazonSecretsManagerClient(credentials, config);
                });

                services.AddSingleton<IAmazonSQS>(sp =>
                {
                    var config = new AmazonSQSConfig
                    {
                        ServiceURL = awsEndpoint,
                        UseHttp = true
                    };
                    return new AmazonSQSClient(credentials, config);
                });

                services.AddSingleton<IAmazonCloudWatch>(sp =>
                {
                    var config = new AmazonCloudWatchConfig
                    {
                        ServiceURL = awsEndpoint,
                        UseHttp = true
                    };
                    return new AmazonCloudWatchClient(credentials, config);
                });
            }
            else
            {
                services.AddSingleton<IAmazonSecretsManager, AmazonSecretsManagerClient>();
            }
        }
        else
        {
            services.AddDbContext<MySqlDbContext>(o => o.UseInMemoryDatabase(ConfigurationConstants.IN_MEMORY_DB_NAME));
            services.AddSingleton<IAmazonSecretsManager, AmazonSecretsManagerClient>();
        }

        if (isIntegrationTestMode)
            services.AddScoped<SnowflakeConnectionProvider, MockSnowflakeConnectionProvider>(sp =>
            {
                var secretsManager = sp.GetRequiredService<IAmazonSecretsManager>();
                var logger = sp.GetRequiredService<ILogger<SnowflakeConnectionProvider>>();
                var config = sp.GetRequiredService<IConfiguration>();
                return new MockSnowflakeConnectionProvider(secretsManager, logger, config);
            });
        else
            // Unit test: Use production provider (but won't actually connect)
            services.AddScoped<SnowflakeConnectionProvider>();

        services.AddScoped<ISnowflakeConnectionProvider>(sp => sp.GetRequiredService<SnowflakeConnectionProvider>());
        services.AddScoped<ISnowflakeItemQueryBuilder, SnowflakeItemQueryBuilder>();

        if (isIntegrationTestMode)
            // Integration test: Use mock repository that returns test data without connecting to Snowflake
            services.AddScoped<ISnowflakeRepository, MockSnowflakeRepository>();
        else
            services.AddScoped<ISnowflakeRepository, SnowflakeRepository>();

        ConfigureSqsOptions(services, configuration);
        var sqsUrl = configuration[ConfigurationConstants.SQS_URL];
        Console.WriteLine($"[DI] SQS options configured. QueueUrl: {sqsUrl ?? "MISSING"}");

        services.AddScoped<IItemMasterLogRepository, EfItemMasterLogRepository>();
        services.AddScoped<IItemPublisher, SqsItemPublisher>();
        services.AddScoped<IMetricsService, CloudWatchMetricsService>();
        services.AddScoped<ITracingService, XRayTracingService>();
        services.AddScoped<IObservabilityService, ObservabilityService>();

        RegisterApplicationServices(services);
    }

    private void RegisterProductionServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAmazonSecretsManager, AmazonSecretsManagerClient>();
        services.AddSingleton<IConnectionStringProvider, SecretsAwareMySqlConnectionStringProvider>();
        services.AddSingleton<IAmazonSQS, AmazonSQSClient>();
        services.AddSingleton<IAmazonCloudWatch, AmazonCloudWatchClient>();

        var validationResult = _configValidationService.ValidateConfiguration(configuration);
        if (!validationResult.IsValid)
            throw new InvalidOperationException(
                $"Configuration validation failed: {string.Join("; ", validationResult.Errors)}");

        ConfigureSqsOptions(services, configuration);
        ConfigureDatabase(services, configuration);
        ConfigureSnowflake(services, configuration);
        services.AddScoped<IMetricsService, CloudWatchMetricsService>();
        services.AddScoped<ITracingService, XRayTracingService>();
        services.AddScoped<IObservabilityService, ObservabilityService>();

        RegisterApplicationServices(services);
    }

    private void RegisterApplicationServices(IServiceCollection services)
    {
        services.AddSingleton<IRequestSourceDetector, RequestSourceDetector>();
        services.AddScoped<IUnifiedItemMapper, UnifiedItemMapper>();
        services.AddScoped<IItemFetchingService, ItemFetchingService>();
        services.AddScoped<IItemMappingService, ItemMappingService>();
        services.AddScoped<IItemPublishingService, ItemPublishingService>();

        services.AddScoped<ISkuAnalysisService, SkuAnalysisService>();
        services.AddScoped<IProcessingResponseBuilder, ProcessingResponseBuilder>();
        services.AddScoped<ISkuProcessingOrchestrator, SkuProcessingOrchestrator>();
        services.AddScoped<IProcessSkusUseCase, ProcessSkusUseCase>();
    }

    private void ConfigureSqsOptions(IServiceCollection services, IConfiguration configuration)
    {
        var queueUrl = configuration[ConfigurationConstants.SQS_URL];
        if (string.IsNullOrWhiteSpace(queueUrl))
            throw new InvalidOperationException(
                $"SQS QueueUrl is required but not configured. Please set {ConfigurationConstants.SQS_URL} environment variable or configuration key (sqs__url).");

        services.Configure<SqsItemPublisherOptions>(opts =>
        {
            opts.QueueUrl = queueUrl;
            opts.MaxRetries = ParseInt(configuration[ConfigurationConstants.SQS_MAX_RETRIES],
                ConfigurationConstants.DEFAULT_MAX_RETRIES, v => v >= 0 && v <= 10);
            opts.BaseDelayMs = ParseInt(configuration[ConfigurationConstants.SQS_BASE_DELAY_MS],
                ConfigurationConstants.DEFAULT_BASE_DELAY_MS, v => v > 0);
            opts.BackoffMultiplier = ParseDouble(configuration[ConfigurationConstants.SQS_BACKOFF_MULTIPLIER],
                ConfigurationConstants.DEFAULT_BACKOFF_MULTIPLIER, v => v > 1.0);
            opts.BatchSize = ParseInt(configuration[ConfigurationConstants.SQS_BATCH_SIZE],
                ConfigurationConstants.DEFAULT_BATCH_SIZE, v => v > 0 && v <= 500);

            opts.CircuitBreakerFailureThreshold = ParseInt(
                configuration[ConfigurationConstants.SQS_CIRCUIT_BREAKER_FAILURE_THRESHOLD],
                ConfigurationConstants.DEFAULT_CIRCUIT_BREAKER_FAILURE_THRESHOLD, v => v > 0 && v <= 20);
            opts.CircuitBreakerDurationOfBreak = TimeSpan.FromSeconds(ParseInt(
                configuration[ConfigurationConstants.SQS_CIRCUIT_BREAKER_DURATION_OF_BREAK_SECONDS],
                ConfigurationConstants.DEFAULT_CIRCUIT_BREAKER_DURATION_OF_BREAK_SECONDS, v => v > 0 && v <= 300));
            opts.CircuitBreakerSamplingDuration = ParseInt(
                configuration[ConfigurationConstants.SQS_CIRCUIT_BREAKER_SAMPLING_DURATION_SECONDS],
                ConfigurationConstants.DEFAULT_CIRCUIT_BREAKER_SAMPLING_DURATION_SECONDS, v => v > 0 && v <= 600);
            opts.CircuitBreakerMinimumThroughput = ParseInt(
                configuration[ConfigurationConstants.SQS_CIRCUIT_BREAKER_MINIMUM_THROUGHPUT],
                ConfigurationConstants.DEFAULT_CIRCUIT_BREAKER_MINIMUM_THROUGHPUT, v => v > 0 && v <= 100);
        });

        services.AddScoped<IItemPublisher, SqsItemPublisher>();
    }

    private void ConfigureDatabase(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = _connectionStringService.GetConnectionString(services.BuildServiceProvider());

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Database connection string is missing or empty");

        var serverVersion = ServerVersion.AutoDetect(connectionString);
        services.AddDbContext<MySqlDbContext>(o => o.UseMySql(connectionString, serverVersion));
        services.AddScoped<IItemMasterLogRepository, MySqlItemMasterLogRepository>();
    }

    private void ConfigureSnowflake(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<SnowflakeConnectionProvider>();

        services.Configure<SnowflakeOptions>(opts =>
        {
            opts.Database = configuration[ConfigurationConstants.SNOWFLAKE_DATABASE]!;
            opts.Schema = configuration[ConfigurationConstants.SNOWFLAKE_SCHEMA]!;
            opts.Table = configuration[ConfigurationConstants.SNOWFLAKE_TABLE]!;
        });

        services.AddScoped<ISnowflakeItemQueryBuilder, SnowflakeItemQueryBuilder>();
        services.AddScoped<ISnowflakeRepository, SnowflakeRepository>();
    }

    private static int ParseInt(string? raw, int defaultValue, Func<int, bool>? predicate = null)
    {
        return int.TryParse(raw, out var v) && (predicate == null || predicate(v)) ? v : defaultValue;
    }

    private static double ParseDouble(string? raw, double defaultValue, Func<double, bool>? predicate = null)
    {
        return double.TryParse(raw, out var v) && (predicate == null || predicate(v)) ? v : defaultValue;
    }
}