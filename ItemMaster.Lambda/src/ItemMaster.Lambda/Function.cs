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

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ItemMaster.Lambda;

public class Function
{
    private static readonly ServiceProvider ServiceProvider;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static bool _migrationsApplied;

    static Function()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IConfigProvider, EnvConfigProvider>();
        services.AddSingleton<IAmazonSecretsManager>(_ => new AmazonSecretsManagerClient());
        services.AddSingleton<IConnectionStringProvider, SecretsAwareMySqlConnectionStringProvider>();

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
                    services.AddScoped<IItemMasterLogRepository, EfItemMasterLogRepository>();
                }
                catch
                {
                    services.AddSingleton<IItemMasterLogRepository>(_ => new MySqlItemMasterLogRepository(connStr!));
                }
            }
            else
            {
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
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Startup] Migration failed: {ex.Message}");
            }
        }
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var logger = context.Logger;
        ProcessSkusRequest? input = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(request.Body))
            {
                var bodyText = request.Body;
                if (request.IsBase64Encoded)
                {
                    try
                    {
                        var bytes = Convert.FromBase64String(bodyText);
                        bodyText = System.Text.Encoding.UTF8.GetString(bytes);
                    }
                    catch (Exception ex)
                    {
                        logger.LogLine($"Failed to base64 decode body: {ex.Message}");
                    }
                }
                input = JsonSerializer.Deserialize<ProcessSkusRequest>(bodyText, JsonOptions);
            }
        }
        catch (Exception ex)
        {
            logger.LogLine($"Failed to deserialize body: {ex.Message}");
        }

        input ??= new ProcessSkusRequest();

        var requestId = context.AwsRequestId ?? Guid.NewGuid().ToString("N");
        using var scope = ServiceProvider.CreateScope();
        var useCase = scope.ServiceProvider.GetRequiredService<IProcessSkusUseCase>();

        ProcessSkusResponse response;
        try
        {
            response = await useCase.ExecuteAsync(input.Skus, source: "api", requestId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogLine($"Use case failed: {ex.Message}");
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Body = JsonSerializer.Serialize(new { error = "internal_error" }, JsonOptions),
                Headers = new Dictionary<string, string>{{"Content-Type","application/json"}}
            };
        }

        var body = JsonSerializer.Serialize(response, JsonOptions);
        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = body,
            Headers = new Dictionary<string, string>{{"Content-Type","application/json"}}
        };
    }
}
