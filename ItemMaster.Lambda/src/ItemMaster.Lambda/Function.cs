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

    static Function()
    {
        ConfigureSerilog();
        var services = new ServiceCollection();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IConfigProvider, EnvConfigProvider>();
        services.AddSingleton<IAmazonSecretsManager>(_ => new AmazonSecretsManagerClient());
        services.AddSingleton<IConnectionStringProvider, SecretsAwareMySqlConnectionStringProvider>();
        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.AddSerilog();
        });

        using (var temp = services.BuildServiceProvider())
        {
            string? connStr = null;
            try { connStr = temp.GetRequiredService<IConnectionStringProvider>().GetMySqlConnectionString(); } catch { }

            if (!string.IsNullOrWhiteSpace(connStr))
            {
                try
                {
                    var serverVersion = ServerVersion.AutoDetect(connStr);
                    services.AddDbContext<ItemMasterDbContext>(o => o.UseMySql(connStr, serverVersion));
                    services.AddScoped<IItemMasterLogRepository, MySqlItemMasterLogRepository>();
                }
                catch (Exception ex)
                {
                    Log.Logger.Warning(ex, "DbContextConfigFailure falling back to in-memory repo");
                    services.AddSingleton<IItemMasterLogRepository, InMemoryItemMasterLogRepository>();
                }
            }
            else
            {
                Log.Logger.Information("NoMySqlConnectionStringUsingInMemoryRepo");
                services.AddSingleton<IItemMasterLogRepository, InMemoryItemMasterLogRepository>();
            }
        }

        services.AddScoped<IProcessSkusUseCase, ProcessSkusUseCase>();

        ServiceProvider = services.BuildServiceProvider();

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
                    Log.Information("MigrationsAppliedOnce");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "MigrationFailure");
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
        using (LogContext.PushProperty("AwsRequestId", context.AwsRequestId))
        {
            var scope = ServiceProvider.CreateScope();
            try
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Function>>();
                var bodyRaw = request.Body;
                logger.LogInformation("HandlerStart hasBody={HasBody} isBase64={IsBase64}", !string.IsNullOrWhiteSpace(bodyRaw), request.IsBase64Encoded);

                ProcessSkusRequest? input = null;
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
                            logger.LogWarning(ex, "Base64DecodeFailure");
                        }
                    }
                    try
                    {
                        input = JsonSerializer.Deserialize<ProcessSkusRequest>(bodyRaw, JsonOptions);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "BodyDeserializationFailure");
                    }
                }
                input ??= new ProcessSkusRequest();

                var requestId = context.AwsRequestId ?? Guid.NewGuid().ToString("N");
                using (LogContext.PushProperty("RequestId", requestId))
                {
                    logger.LogInformation("ParsedSkus count={Count}", input.Skus.Count);
                    var useCase = scope.ServiceProvider.GetRequiredService<IProcessSkusUseCase>();
                    ProcessSkusResponse response;
                    try
                    {
                        response = await useCase.ExecuteAsync(input.Skus, source: "api", requestId, CancellationToken.None);
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
                    logger.LogInformation("HandlerComplete logged={Logged} failed={Failed}", response.Logged, response.Failed);
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = 200,
                        Body = JsonSerializer.Serialize(response, JsonOptions),
                        Headers = new Dictionary<string, string>{{"Content-Type","application/json"}}
                    };
                }
            }
            finally
            {
                scope.Dispose();
            }
        }
    }
}
