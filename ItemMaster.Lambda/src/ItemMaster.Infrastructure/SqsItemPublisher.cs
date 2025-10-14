using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using ItemMaster.Contracts;
using ItemMaster.Domain;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace ItemMaster.Infrastructure;

public class SqsItemPublisher : IItemPublisher
{
    private readonly ILogger<SqsItemPublisher> _logger;
    private readonly IItemMasterLogRepository _logRepository;
    private readonly SqsItemPublisherOptions _options;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly IAmazonSQS _sqsClient;

    public SqsItemPublisher(
        IAmazonSQS sqsClient,
        IOptions<SqsItemPublisherOptions> options,
        ILogger<SqsItemPublisher> logger,
        IItemMasterLogRepository logRepository)
    {
        _sqsClient = sqsClient;
        _options = options.Value;
        _logger = logger;
        _logRepository = logRepository;
        _resiliencePipeline = CreateResiliencePipeline();
    }

    public async Task<Result> PublishItemsAsync(IEnumerable<Item> items, string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();
        _logger.LogInformation("Starting to publish {ItemCount} items to SQS", itemList.Count);

        try
        {
            var batches = CreateBatchesFromItems(itemList);
            var publishResults = new List<BatchPublishResult>();

            foreach (var batch in batches)
            {
                var result = await PublishBatchWithCircuitBreaker(batch, cancellationToken);
                publishResults.Add(result);

                await LogBatchResults(result, "PublishItems", traceId, cancellationToken);
            }

            var totalSuccessful = publishResults.Sum(r => r.SuccessfulMessages.Count);
            var totalFailed = publishResults.Sum(r => r.FailedMessages.Count);

            _logger.LogInformation("SQS publishing completed: {SuccessCount} successful, {FailedCount} failed",
                totalSuccessful, totalFailed);

            // Log overall result to MySQL
            await _logRepository.LogProcessingResultAsync(
                "SQS_PUBLISH_ITEMS",
                totalFailed == 0,
                RequestSource.Lambda,
                totalFailed > 0 ? $"Failed to publish {totalFailed} out of {itemList.Count} items" : null,
                totalSuccessful,
                traceId,
                cancellationToken);

            return totalFailed == 0
                ? Result.Success()
                : Result.Failure($"Failed to publish {totalFailed} out of {itemList.Count} items to SQS");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while publishing items to SQS");
            await _logRepository.LogProcessingResultAsync(
                "SQS_PUBLISH_ITEMS",
                false,
                RequestSource.Lambda,
                ex.Message,
                null,
                traceId,
                cancellationToken);
            return Result.Failure($"SQS publish error: {ex.Message}");
        }
    }

    public async Task<Result> PublishSimplifiedItemsAsync(IEnumerable<ItemForSqs> items, string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();
        _logger.LogInformation("Starting to publish {ItemCount} simplified items to SQS", itemList.Count);

        try
        {
            var batches = CreateBatchesFromSimplifiedItems(itemList);
            var publishResults = new List<BatchPublishResult>();

            foreach (var batch in batches)
            {
                var result = await PublishBatchWithCircuitBreaker(batch, cancellationToken);
                publishResults.Add(result);

                await LogBatchResults(result, "PublishSimplifiedItems", traceId, cancellationToken);
            }

            var totalSuccessful = publishResults.Sum(r => r.SuccessfulMessages.Count);
            var totalFailed = publishResults.Sum(r => r.FailedMessages.Count);

            _logger.LogInformation(
                "SQS simplified items publishing completed: {SuccessCount} successful, {FailedCount} failed",
                totalSuccessful, totalFailed);

            await _logRepository.LogProcessingResultAsync(
                "SQS_PUBLISH_SIMPLIFIED_ITEMS",
                totalFailed == 0,
                RequestSource.Lambda,
                totalFailed > 0 ? $"Failed to publish {totalFailed} out of {itemList.Count} simplified items" : null,
                totalSuccessful,
                traceId,
                cancellationToken);

            return totalFailed == 0
                ? Result.Success()
                : Result.Failure($"Failed to publish {totalFailed} out of {itemList.Count} simplified items to SQS");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while publishing simplified items to SQS");
            await _logRepository.LogProcessingResultAsync(
                "SQS_PUBLISH_SIMPLIFIED_ITEMS",
                false,
                RequestSource.Lambda,
                ex.Message,
                null,
                traceId,
                cancellationToken);
            return Result.Failure($"SQS publish error: {ex.Message}");
        }
    }

    private ResiliencePipeline CreateResiliencePipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                MaxRetryAttempts = _options.MaxRetries,
                Delay = TimeSpan.FromMilliseconds(_options.BaseDelayMs),
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Retrying SQS publish attempt {AttemptNumber} after {Delay}ms due to: {Exception}",
                        args.AttemptNumber, args.RetryDelay.TotalMilliseconds, args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                FailureRatio = 0.5,
                MinimumThroughput = _options.CircuitBreakerMinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(_options.CircuitBreakerSamplingDuration),
                BreakDuration = _options.CircuitBreakerDurationOfBreak,
                OnOpened = _ =>
                {
                    _logger.LogError("SQS Circuit breaker opened due to failures. Will remain open for {BreakDuration}",
                        _options.CircuitBreakerDurationOfBreak);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("SQS Circuit breaker closed. Normal operations resumed.");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    _logger.LogInformation("SQS Circuit breaker half-opened. Testing if service is healthy.");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    private async Task<BatchPublishResult> PublishBatchWithCircuitBreaker(List<SendMessageBatchRequestEntry> batch,
        CancellationToken cancellationToken)
    {
        var remainingMessages = new List<SendMessageBatchRequestEntry>(batch);
        var allSuccessfulMessages = new List<SendMessageBatchRequestEntry>();
        var allFailures = new List<BatchResultErrorEntry>();
        var retryAttempt = 0;
        var maxRetries = _options.MaxRetries;

        while (remainingMessages.Any() && retryAttempt <= maxRetries)
            try
            {
                var result = await _resiliencePipeline.ExecuteAsync(async _ =>
                {
                    var request = new SendMessageBatchRequest
                    {
                        QueueUrl = _options.QueueUrl,
                        Entries = remainingMessages
                    };

                    var response = await _sqsClient.SendMessageBatchAsync(request, cancellationToken);
                    return response;
                }, cancellationToken);

                var successfulInThisAttempt = remainingMessages.Where(entry =>
                    result.Failed?.All(failed => failed.Id != entry.Id) ?? true).ToList();
                var failedInThisAttempt = remainingMessages.Where(entry =>
                    result.Failed?.Any(failed => failed.Id == entry.Id) ?? false).ToList();

                allSuccessfulMessages.AddRange(successfulInThisAttempt);

                if (result.Failed?.Any() ?? false)
                {
                    allFailures.AddRange(result.Failed);

                    if (failedInThisAttempt.Any() && retryAttempt < maxRetries)
                    {
                        _logger.LogWarning(
                            "Batch had {FailedCount} failed messages out of {TotalCount}. Will retry failed messages (Attempt {Attempt}/{MaxRetries})",
                            failedInThisAttempt.Count, remainingMessages.Count, retryAttempt + 1, maxRetries);

                        remainingMessages = failedInThisAttempt;
                        retryAttempt++;

                        var delay = TimeSpan.FromMilliseconds(_options.BaseDelayMs * Math.Pow(2, retryAttempt));
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }
                }

                return new BatchPublishResult
                {
                    SuccessfulMessages = allSuccessfulMessages,
                    FailedMessages = failedInThisAttempt,
                    SqsFailures = allFailures
                };
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogError("SQS Circuit breaker is open, skipping batch of {BatchSize} messages",
                    remainingMessages.Count);
                return new BatchPublishResult
                {
                    SuccessfulMessages = allSuccessfulMessages,
                    FailedMessages = remainingMessages,
                    SqsFailures = allFailures,
                    CircuitBreakerTripped = true,
                    Exception = ex
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing batch of {BatchSize} messages (Attempt {Attempt}/{MaxRetries})",
                    remainingMessages.Count, retryAttempt + 1, maxRetries);

                if (retryAttempt < maxRetries)
                {
                    retryAttempt++;
                    var delay = TimeSpan.FromMilliseconds(_options.BaseDelayMs * Math.Pow(2, retryAttempt));
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                return new BatchPublishResult
                {
                    SuccessfulMessages = allSuccessfulMessages,
                    FailedMessages = remainingMessages,
                    SqsFailures = allFailures,
                    Exception = ex
                };
            }

        _logger.LogError("Exhausted all {MaxRetries} retry attempts. {FailedCount} messages could not be published",
            maxRetries, remainingMessages.Count);

        return new BatchPublishResult
        {
            SuccessfulMessages = allSuccessfulMessages,
            FailedMessages = remainingMessages,
            SqsFailures = allFailures
        };
    }

    private async Task LogBatchResults(BatchPublishResult result, string operation, string? traceId,
        CancellationToken cancellationToken)
    {
        foreach (var successfulMessage in result.SuccessfulMessages)
            _logger.LogDebug("Successfully published message {MessageId} to SQS", successfulMessage.Id);

        foreach (var failedMessage in result.FailedMessages)
        {
            var errorDetail = result.SqsFailures.FirstOrDefault(f => f.Id == failedMessage.Id);
            var errorMessage = result.CircuitBreakerTripped
                ? "Circuit breaker is open"
                : result.Exception?.Message
                  ?? errorDetail?.Message
                  ?? "Unknown error";

            _logger.LogError("Failed to publish message {MessageId} to SQS: {ErrorCode} - {ErrorMessage}",
                failedMessage.Id, errorDetail?.Code ?? "UNKNOWN", errorMessage);

            await _logRepository.LogProcessingResultAsync(
                $"SQS_MESSAGE_PUBLISH_{operation.ToUpper()}",
                false,
                RequestSource.Lambda,
                $"MessageId: {failedMessage.Id}, Error: {errorMessage}",
                0,
                traceId,
                cancellationToken);
        }

        if (result.SuccessfulMessages.Any())
            await _logRepository.LogProcessingResultAsync(
                $"SQS_MESSAGE_PUBLISH_{operation.ToUpper()}",
                true,
                RequestSource.Lambda,
                null,
                result.SuccessfulMessages.Count,
                traceId,
                cancellationToken);
    }

    private List<List<SendMessageBatchRequestEntry>> CreateBatchesFromItems(List<Item> items)
    {
        var batches = new List<List<SendMessageBatchRequestEntry>>();
        const int batchSize = 10;

        for (var i = 0; i < items.Count; i += batchSize)
        {
            var batch = new List<SendMessageBatchRequestEntry>();
            var itemsBatch = items.Skip(i).Take(batchSize);

            foreach (var item in itemsBatch)
                batch.Add(new SendMessageBatchRequestEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    MessageBody = JsonSerializer.Serialize(item)
                });

            batches.Add(batch);
        }

        return batches;
    }

    private List<List<SendMessageBatchRequestEntry>> CreateBatchesFromSimplifiedItems(List<ItemForSqs> items)
    {
        var batches = new List<List<SendMessageBatchRequestEntry>>();
        const int batchSize = 10;

        for (var i = 0; i < items.Count; i += batchSize)
        {
            var batch = new List<SendMessageBatchRequestEntry>();
            var itemsBatch = items.Skip(i).Take(batchSize);

            foreach (var item in itemsBatch)
                batch.Add(new SendMessageBatchRequestEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    MessageBody = JsonSerializer.Serialize(item)
                });

            batches.Add(batch);
        }

        return batches;
    }
}