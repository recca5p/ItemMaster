using Amazon.SQS;
using Amazon.SQS.Model;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ItemMaster.Infrastructure;

public sealed class SqsItemPublisher : IItemPublisher
{
    private readonly IAmazonSQS _sqs;
    private readonly ILogger<SqsItemPublisher> _logger;
    private readonly string _queueUrl;
    private readonly int _maxRetries;
    private readonly int _baseDelayMs;
    private readonly double _backoffMultiplier;
    private readonly int _batchSize;

    public SqsItemPublisher(IAmazonSQS sqs, SqsItemPublisherOptions options, ILogger<SqsItemPublisher> logger)
    {
        _sqs = sqs ?? throw new ArgumentNullException(nameof(sqs));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _queueUrl = options.QueueUrl;
        _maxRetries = options.MaxRetries;
        _baseDelayMs = options.BaseDelayMs;
        _backoffMultiplier = options.BackoffMultiplier;
        _batchSize = options.BatchSize;
        if (string.IsNullOrWhiteSpace(_queueUrl)) throw new ArgumentException("QueueUrl required", nameof(options.QueueUrl));
    }

    public async Task<int> PublishAsync(IEnumerable<string> skus, string source, string requestId, CancellationToken cancellationToken)
    {
        var list = skus.ToList();
        if (list.Count == 0) return 0;

        var total = 0;
        for (int i = 0; i < list.Count; i += _batchSize)
        {
            var batch = list.Skip(i).Take(_batchSize).ToList();
            var entries = batch.Select((sku, idx) => new SendMessageBatchRequestEntry
            {
                Id = ($"{i}_{idx}").Replace('-', '_'),
                MessageBody = JsonSerializer.Serialize(new { sku, source, requestId })
            }).ToList();

            var publishedCount = await PublishBatchWithRetryAsync(entries, cancellationToken);
            total += publishedCount;
        }
        return total;
    }

    private async Task<int> PublishBatchWithRetryAsync(List<SendMessageBatchRequestEntry> entries, CancellationToken cancellationToken)
    {
        var attempt = 0;
        var remainingEntries = entries.ToList();

        while (attempt <= _maxRetries && remainingEntries.Count > 0)
        {
            try
            {
                if (attempt > 0)
                {
                    var delayMs = (int)(_baseDelayMs * Math.Pow(_backoffMultiplier, attempt - 1));
                    _logger.LogInformation("Retrying SQS publish attempt {Attempt} after {DelayMs}ms delay", attempt, delayMs);
                    await Task.Delay(delayMs, cancellationToken);
                }

                var request = new SendMessageBatchRequest
                {
                    QueueUrl = _queueUrl,
                    Entries = remainingEntries
                };

                var response = await _sqs.SendMessageBatchAsync(request, cancellationToken);
                var successfulCount = response.Successful.Count;
                _logger.LogInformation("Successfully published {Count} messages in batch", successfulCount);

                if (response.Failed.Count > 0)
                {
                    var failedIds = response.Failed.Select(f => f.Id).ToHashSet();
                    remainingEntries = remainingEntries.Where(e => failedIds.Contains(e.Id)).ToList();
                    _logger.LogWarning("Failed to publish {Count} messages, will retry. Failed IDs: {FailedIds}", response.Failed.Count, string.Join(", ", failedIds));
                    foreach (var failed in response.Failed)
                    {
                        _logger.LogError("Message {Id} failed: {Code} - {Message}", failed.Id, failed.Code, failed.Message);
                    }
                }
                else
                {
                    return successfulCount;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SQS publish attempt {Attempt} failed completely", attempt);
                if (attempt == _maxRetries)
                {
                    _logger.LogError("All {MaxRetries} retry attempts exhausted for SQS publish", _maxRetries);
                    throw;
                }
            }
            attempt++;
        }
        return entries.Count - remainingEntries.Count;
    }
}
