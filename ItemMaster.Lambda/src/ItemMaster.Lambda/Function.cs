using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.XRay.Recorder.Handlers.AwsSdk;
using ItemMaster.Lambda.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace ItemMaster.Lambda;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public class Function
{
    // Configuration constants
    private const string STARTUP_ERROR_MESSAGE = "startup";
    private const string UNKNOWN_TRACE_ID = "unknown";

    private static readonly ServiceProvider? ServiceProvider;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IFunctionStartupService StartupService;
    private static readonly ILambdaRequestHandler? RequestHandler;

    static Function()
    {
        AWSSDKHandler.RegisterXRayForAllServices();

        StartupService = new FunctionStartupService();

        try
        {
            ServiceProvider = StartupService.InitializeServices();
            RequestHandler = new LambdaRequestHandler(ServiceProvider);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Function static initialization failed");
        }
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(object input, ILambdaContext context)
    {
        if (StartupService.HasStartupError || RequestHandler == null)
        {
            var responseService = new ResponseService();
            return responseService.CreateErrorResponse(
                StartupService.StartupErrorMessage ?? STARTUP_ERROR_MESSAGE,
                UNKNOWN_TRACE_ID);
        }

        return await RequestHandler.HandleRequestAsync(input, context);
    }
}