using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ItemMaster.Lambda.Services;

public interface IFunctionStartupService
{
    bool HasStartupError { get; }
    string? StartupErrorMessage { get; }
    ServiceProvider InitializeServices();
}

[ExcludeFromCodeCoverage]
public class FunctionStartupService : IFunctionStartupService
{
    // Configuration constants
    private const string STARTUP_INITIALIZATION_FAILED = "startup_initialization_failed";
    private const string MIGRATION_FAILURE = "migration_failure";

    public bool HasStartupError { get; private set; }
    public string? StartupErrorMessage { get; private set; }

    public ServiceProvider InitializeServices()
    {
        try
        {
            var serviceProvider = BuildServiceProvider();
            ApplyMigrationsIfNeeded(serviceProvider);
            return serviceProvider;
        }
        catch (Exception ex)
        {
            HasStartupError = true;
            StartupErrorMessage = STARTUP_INITIALIZATION_FAILED;
            Log.Error(ex, "Function startup failed");
            throw;
        }
    }

    private ServiceProvider BuildServiceProvider()
    {
        var configurationService = new ConfigurationService();
        var loggingService = new LoggingService();

        var startupOptions = configurationService.GetStartupOptions();
        var configuration = configurationService.BuildConfiguration(startupOptions);

        loggingService.ConfigureSerilog(configuration);

        var configValidationService = new ConfigurationValidationService();
        var connectionStringService = new ConnectionStringService(
            LoggerFactory.Create(builder => builder.AddSerilog())
                .CreateLogger<ConnectionStringService>());

        var dependencyInjectionService = new DependencyInjectionService(
            configValidationService,
            connectionStringService);

        return dependencyInjectionService.BuildServiceProvider(configuration, startupOptions.TestMode);
    }

    private void ApplyMigrationsIfNeeded(ServiceProvider serviceProvider)
    {
        var configurationService = new ConfigurationService();
        var startupOptions = configurationService.GetStartupOptions();

        if (!startupOptions.ApplyMigrations || startupOptions.TestMode)
            return;

        try
        {
            using var scope = serviceProvider.CreateScope();
            var migrationService = scope.ServiceProvider.GetService<IDatabaseMigrationService>();
            migrationService?.ApplyMigrations();
        }
        catch (Exception ex)
        {
            HasStartupError = true;
            StartupErrorMessage = MIGRATION_FAILURE;
            Log.Error(ex, "Database migration failed");
            throw;
        }
    }
}