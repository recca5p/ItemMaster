using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using ItemMaster.Application;
using ItemMaster.Infrastructure.Observability;
using ItemMaster.Shared;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace ItemMaster.Lambda.Services;

public interface ILambdaRequestHandler
{
    Task<APIGatewayProxyResponse> HandleRequestAsync(object input, ILambdaContext context);
}

[ExcludeFromCodeCoverage]
public class LambdaRequestHandler : ILambdaRequestHandler
{
    // Configuration constants
    private const int LAMBDA_TIMEOUT_BUFFER_SECONDS = 30;

    private readonly ServiceProvider _serviceProvider;

    public LambdaRequestHandler(ServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<APIGatewayProxyResponse> HandleRequestAsync(object input, ILambdaContext context)
    {
        using var scope = _serviceProvider.CreateScope();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<LambdaRequestHandler>>();
        var requestSourceDetector = scope.ServiceProvider.GetRequiredService<IRequestSourceDetector>();
        var requestProcessingService = scope.ServiceProvider.GetRequiredService<IRequestProcessingService>();
        var responseService = scope.ServiceProvider.GetRequiredService<IResponseService>();
        var observabilityService = scope.ServiceProvider.GetRequiredService<IObservabilityService>();

        var requestSource = requestSourceDetector.DetectSource(input);

        if (IsHealthCheckRequest(requestSource)) return HandleHealthCheckRequest(responseService, logger);

        return await ProcessBusinessRequest(
            input, context, requestSource, logger, requestProcessingService,
            responseService, observabilityService, scope);
    }

    private static bool IsHealthCheckRequest(RequestSource requestSource)
    {
        return requestSource == RequestSource.CicdHealthCheck;
    }

    private static APIGatewayProxyResponse HandleHealthCheckRequest(
        IResponseService responseService,
        ILogger<LambdaRequestHandler> logger)
    {
        var healthResponse = responseService.CreateHealthCheckResponse();
        logger.LogInformation("Health check: {response}", healthResponse.Body);
        return healthResponse;
    }

    private async Task<APIGatewayProxyResponse> ProcessBusinessRequest(
        object input,
        ILambdaContext context,
        RequestSource requestSource,
        ILogger<LambdaRequestHandler> logger,
        IRequestProcessingService requestProcessingService,
        IResponseService responseService,
        IObservabilityService observabilityService,
        IServiceScope scope)
    {
        var traceId = observabilityService.GetCurrentTraceId();

        using (LogContext.PushProperty("AwsRequestId", context.AwsRequestId))
        using (LogContext.PushProperty("RequestSource", requestSource.ToString()))
        using (LogContext.PushProperty("TraceId", traceId))
        {
            return await observabilityService.ExecuteWithObservabilityAsync(
                "LambdaHandler",
                requestSource,
                async () => await ExecuteBusinessLogic(
                    input, context, requestSource, traceId, logger,
                    requestProcessingService, responseService, scope),
                CreateLambdaMetadata(context));
        }
    }

    private async Task<APIGatewayProxyResponse> ExecuteBusinessLogic(
        object input,
        ILambdaContext context,
        RequestSource requestSource,
        string traceId,
        ILogger<LambdaRequestHandler> logger,
        IRequestProcessingService requestProcessingService,
        IResponseService responseService,
        IServiceScope scope)
    {
        logger.LogInformation("Processing request from source: {RequestSource} | TraceId: {TraceId}",
            requestSource, traceId);

        var processRequest = requestProcessingService.ParseRequest(input, requestSource, traceId);
        var useCase = scope.ServiceProvider.GetRequiredService<IProcessSkusUseCase>();

        var cancellationToken = CreateCancellationToken(context);
        var result = await useCase.ExecuteAsync(processRequest, requestSource, traceId, cancellationToken);

        if (result.IsSuccess)
        {
            logger.LogInformation("Lambda execution completed successfully | TraceId: {TraceId}", traceId);
            return responseService.CreateSuccessResponse(result.Value, traceId);
        }

        logger.LogError("ProcessSkus failed: {Error} | TraceId: {TraceId}", result.ErrorMessage, traceId);
        return responseService.CreateErrorResponse(result.ErrorMessage, traceId);
    }

    private static CancellationToken CreateCancellationToken(ILambdaContext context)
    {
        return context.RemainingTime > TimeSpan.FromSeconds(LAMBDA_TIMEOUT_BUFFER_SECONDS)
            ? new CancellationTokenSource(
                context.RemainingTime.Subtract(TimeSpan.FromSeconds(LAMBDA_TIMEOUT_BUFFER_SECONDS))).Token
            : CancellationToken.None;
    }

    private static Dictionary<string, object> CreateLambdaMetadata(ILambdaContext context)
    {
        return new Dictionary<string, object>
        {
            ["awsRequestId"] = context.AwsRequestId,
            ["functionName"] = context.FunctionName,
            ["functionVersion"] = context.FunctionVersion,
            ["memoryLimitInMB"] = context.MemoryLimitInMB,
            ["remainingTimeMs"] = context.RemainingTime.TotalMilliseconds
        };
    }
}