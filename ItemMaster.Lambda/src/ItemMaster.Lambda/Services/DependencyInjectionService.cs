using Amazon.CloudWatch;
using Amazon.SecretsManager;
using Amazon.SQS;
using ItemMaster.Application;
using ItemMaster.Application.Services;
using ItemMaster.Infrastructure;
using ItemMaster.Infrastructure.Ef;
using ItemMaster.Infrastructure.Observability;
using ItemMaster.Infrastructure.Secrets;
using ItemMaster.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using ItemMaster.Lambda.Configuration;

namespace ItemMaster.Lambda.Services;

public interface IDependencyInjectionService
{
    ServiceProvider BuildServiceProvider(IConfiguration configuration, bool isTestMode);
}

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

        // Register core services first
        RegisterCoreServices(services, configuration);
        
        // Register Lambda-specific services
        RegisterLambdaServices(services);

        if (isTestMode)
        {
            RegisterTestModeServices(services);
        }
        else
        {
            RegisterProductionServices(services, configuration);
        }

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

        // Configure IOptions pattern for all configuration sections
        RegisterConfigurationOptions(services, configuration);
    }

    private static void RegisterConfigurationOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CacheConfiguration>(configuration.GetSection(CacheConfiguration.SECTION_NAME));
        services.Configure<DatabaseConfiguration>(configuration.GetSection(DatabaseConfiguration.SECTION_NAME));
        services.Configure<ObservabilityConfiguration>(configuration.GetSection(ObservabilityConfiguration.SECTION_NAME));
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

    private void RegisterTestModeServices(IServiceCollection services)
    {
        // Database - In-memory for testing
        services.AddDbContext<MySqlDbContext>(o => o.UseInMemoryDatabase(ConfigurationConstants.IN_MEMORY_DB_NAME));

        // Note: Test implementations are now in the Test project
        // For unit testing, use the TestImplementations from ItemMaster.Lambda.Tests.Infrastructure
        
        // Use production implementations with in-memory database for integration testing
        services.AddScoped<IItemMasterLogRepository, EfItemMasterLogRepository>();
        services.AddScoped<IItemPublisher, SqsItemPublisher>();
        services.AddScoped<ISnowflakeRepository, SnowflakeRepository>();
        services.AddScoped<IMetricsService, CloudWatchMetricsService>();
        services.AddScoped<ITracingService, XRayTracingService>();
        services.AddScoped<IObservabilityService, ObservabilityService>();

        // Application services
        RegisterApplicationServices(services);
    }

    private void RegisterProductionServices(IServiceCollection services, IConfiguration configuration)
    {
        // AWS services
        services.AddSingleton<IAmazonSecretsManager, AmazonSecretsManagerClient>();
        services.AddSingleton<IConnectionStringProvider, SecretsAwareMySqlConnectionStringProvider>();
        services.AddSingleton<IAmazonSQS, AmazonSQSClient>();
        services.AddSingleton<IAmazonCloudWatch, AmazonCloudWatchClient>();

        // Configuration validation
        var validationResult = _configValidationService.ValidateConfiguration(configuration);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException($"Configuration validation failed: {string.Join("; ", validationResult.Errors)}");
        }

        // Configure SQS options
        ConfigureSqsOptions(services, configuration);

        // Database configuration
        ConfigureDatabase(services, configuration);

        // Snowflake configuration
        ConfigureSnowflake(services, configuration);

        // Observability services
        services.AddScoped<IMetricsService, CloudWatchMetricsService>();
        services.AddScoped<ITracingService, XRayTracingService>();
        services.AddScoped<IObservabilityService, ObservabilityService>();

        // Application services
        RegisterApplicationServices(services);
    }

    private void RegisterApplicationServices(IServiceCollection services)
    {
        services.AddSingleton<IRequestSourceDetector, RequestSourceDetector>();
        services.AddScoped<IUnifiedItemMapper, UnifiedItemMapper>();
        services.AddScoped<IItemFetchingService, ItemFetchingService>();
        services.AddScoped<IItemMappingService, ItemMappingService>();
        services.AddScoped<IItemPublishingService, ItemPublishingService>();
        
        // New split services following clean architecture
        services.AddScoped<ISkuAnalysisService, SkuAnalysisService>();
        services.AddScoped<IProcessingResponseBuilder, ProcessingResponseBuilder>();
        services.AddScoped<ISkuProcessingOrchestrator, SkuProcessingOrchestrator>();
        
        // Main use case - now much simpler
        services.AddScoped<IProcessSkusUseCase, ProcessSkusUseCase>();
    }

    private void ConfigureSqsOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SqsItemPublisherOptions>(opts =>
        {
            opts.QueueUrl = configuration[ConfigurationConstants.SQS_URL]!;
            opts.MaxRetries = ParseInt(configuration[ConfigurationConstants.SQS_MAX_RETRIES], 
                ConfigurationConstants.DEFAULT_MAX_RETRIES, v => v >= 0 && v <= 10);
            opts.BaseDelayMs = ParseInt(configuration[ConfigurationConstants.SQS_BASE_DELAY_MS], 
                ConfigurationConstants.DEFAULT_BASE_DELAY_MS, v => v > 0);
            opts.BackoffMultiplier = ParseDouble(configuration[ConfigurationConstants.SQS_BACKOFF_MULTIPLIER], 
                ConfigurationConstants.DEFAULT_BACKOFF_MULTIPLIER, v => v > 1.0);
            opts.BatchSize = ParseInt(configuration[ConfigurationConstants.SQS_BATCH_SIZE], 
                ConfigurationConstants.DEFAULT_BATCH_SIZE, v => v > 0 && v <= 500);

            opts.CircuitBreakerFailureThreshold = ParseInt(configuration[ConfigurationConstants.SQS_CIRCUIT_BREAKER_FAILURE_THRESHOLD], 
                ConfigurationConstants.DEFAULT_CIRCUIT_BREAKER_FAILURE_THRESHOLD, v => v > 0 && v <= 20);
            opts.CircuitBreakerDurationOfBreak = TimeSpan.FromSeconds(ParseInt(configuration[ConfigurationConstants.SQS_CIRCUIT_BREAKER_DURATION_OF_BREAK_SECONDS], 
                ConfigurationConstants.DEFAULT_CIRCUIT_BREAKER_DURATION_OF_BREAK_SECONDS, v => v > 0 && v <= 300));
            opts.CircuitBreakerSamplingDuration = ParseInt(configuration[ConfigurationConstants.SQS_CIRCUIT_BREAKER_SAMPLING_DURATION_SECONDS], 
                ConfigurationConstants.DEFAULT_CIRCUIT_BREAKER_SAMPLING_DURATION_SECONDS, v => v > 0 && v <= 600);
            opts.CircuitBreakerMinimumThroughput = ParseInt(configuration[ConfigurationConstants.SQS_CIRCUIT_BREAKER_MINIMUM_THROUGHPUT], 
                ConfigurationConstants.DEFAULT_CIRCUIT_BREAKER_MINIMUM_THROUGHPUT, v => v > 0 && v <= 100);
        });

        services.AddScoped<IItemPublisher, SqsItemPublisher>();
    }

    private void ConfigureDatabase(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = _connectionStringService.GetConnectionString(services.BuildServiceProvider());
        
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Database connection string is missing or empty");
        }

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
