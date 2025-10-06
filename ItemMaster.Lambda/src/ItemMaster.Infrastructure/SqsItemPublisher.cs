using Amazon.SQS;
using Amazon.SQS.Model;
using ItemMaster.Shared;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ItemMaster.Infrastructure;

public sealed class SqsPublisherOptions
{
    public string QueueUrl { get; init; } = string.Empty;
    public int MaxRetries { get; init; } = 2;
}

public sealed class SqsItemPublisher : IItemPublisher
{
    private readonly IAmazonSQS _sqs;
    private readonly ILogger<SqsItemPublisher> _logger;
    private readonly string _queueUrl;
    private readonly int _maxRetries;

    public SqsItemPublisher(IAmazonSQS sqs, ILogger<SqsItemPublisher> logger, SqsPublisherOptions options)
    {
        _sqs = sqs;
        _logger = logger;
        _queueUrl = options.QueueUrl;
        _maxRetries = options.MaxRetries;
        if (string.IsNullOrWhiteSpace(_queueUrl)) throw new ArgumentException("QueueUrl required", nameof(options.QueueUrl));
    }

    public async Task<int> PublishAsync(IEnumerable<string> skus, string source, string requestId, CancellationToken ct = default)
    {
        var list = skus.ToList();
        if (list.Count == 0) return 0;
        var total = 0;
        const int batchSize = 10;
        for (int i = 0; i < list.Count; i += batchSize)
        {
            var batch = list.Skip(i).Take(batchSize).ToList();
            var entries = batch.Select((sku, idx) => new SendMessageBatchRequestEntry
            {
                Id = ($"{i}_{idx}").Replace('-', '_'),
                MessageBody = JsonSerializer.Serialize(new { sku, source, requestId })
            }).ToList();
            var attempt = 0;
            while (true)
            {
                try
                {
                    var resp = await _sqs.SendMessageBatchAsync(new SendMessageBatchRequest
                    {
                        QueueUrl = _queueUrl,
                        Entries = entries
                    }, ct);
                    total += resp.Successful.Count;
                    if (resp.Failed.Count > 0)
                        _logger.LogError("SqsPartialFailure failed={Failed} batchStart={Start} attempt={Attempt}", resp.Failed.Count, i, attempt);
                    break;
                }
                catch (Exception ex) when (attempt < _maxRetries)
                {
                    attempt++;
                    _logger.LogError(ex, "SqsBatchRetry batchStart={Start} attempt={Attempt}", i, attempt);
                    await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct);
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SqsBatchFailure batchStart={Start}", i);
                    break;
                }
            }
        }
        return total;
    }
}

public sealed class InMemoryItemPublisher : IItemPublisher
{
    public Task<int> PublishAsync(IEnumerable<string> skus, string source, string requestId, CancellationToken ct = default)
        => Task.FromResult(skus.Count());
}
