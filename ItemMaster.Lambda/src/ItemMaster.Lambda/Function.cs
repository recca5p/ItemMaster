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

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ItemMaster.Lambda;

public class Function
{
    private static readonly ServiceProvider ServiceProvider;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static bool _migrationsApplied;
    private static bool _startupError;
    private static string? _startupErrorMessage;

    static Function()
    {
        ConfigureSerilog();
        var services = new ServiceCollection();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IConfigProvider, EnvConfigProvider>();
        services.AddSingleton<IAmazonSecretsManager>(_ => new AmazonSecretsManagerClient());
        services.AddSingleton<IConnectionStringProvider, SecretsAwareMySqlConnectionStringProvider>();
        services.AddLogging(b => { b.ClearProviders(); b.AddSerilog(); });

        string? connStr = null;
        try
        {
            using var temp = services.BuildServiceProvider();
            connStr = temp.GetRequiredService<IConnectionStringProvider>().GetMySqlConnectionString();
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
                    services.AddDbContext<ItemMasterDbContext>(o => o.UseMySql(connStr, serverVersion));
                    services.AddScoped<IItemMasterLogRepository, MySqlItemMasterLogRepository>();
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
            services.AddScoped<IProcessSkusUseCase, ProcessSkusUseCase>();

        ServiceProvider = services.BuildServiceProvider();

        if (!_startupError)
        {
            var apply = Environment.GetEnvironmentVariable("APPLY_MIGRATIONS");
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

    private static void ConfigureSerilog()
    {
        if (Log.Logger is not Serilog.Core.Logger l || l == Serilog.Core.Logger.None)
        {
            var level = Environment.GetEnvironmentVariable("LOG_LEVEL") ?? "Information";
            var parsed = level switch
            {
                string s when s.Equals("debug", StringComparison.OrdinalIgnoreCase) => Serilog.Events.LogEventLevel.Debug,
                string s when s.Equals("verbose", StringComparison.OrdinalIgnoreCase) => Serilog.Events.LogEventLevel.Verbose,
                string s when s.Equals("warning", StringComparison.OrdinalIgnoreCase) => Serilog.Events.LogEventLevel.Warning,
                string s when s.Equals("error", StringComparison.OrdinalIgnoreCase) => Serilog.Events.LogEventLevel.Error,
                string s when s.Equals("fatal", StringComparison.OrdinalIgnoreCase) => Serilog.Events.LogEventLevel.Fatal,
                _ => Serilog.Events.LogEventLevel.Information
            };
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(parsed)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", "ItemMasterLambda")
                .WriteTo.Console(new RenderedCompactJsonFormatter())
                .CreateLogger();
        }
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
