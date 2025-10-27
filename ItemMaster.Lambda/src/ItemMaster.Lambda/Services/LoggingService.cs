using ItemMaster.Lambda.Configuration;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace ItemMaster.Lambda.Services;

public interface ILoggingService
{
    void ConfigureSerilog(IConfiguration? configuration);
}

public class LoggingService : ILoggingService
{
    public void ConfigureSerilog(IConfiguration? configuration)
    {
        if (Log.Logger is Logger l && l != Logger.None) return;

        var levelRaw = configuration?[ConfigurationConstants.LOG_LEVEL] ?? ConfigurationConstants.DEFAULT_LOG_LEVEL;
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
}