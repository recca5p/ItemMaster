using Amazon.SQS.Model;

namespace ItemMaster.Infrastructure;

public class BatchPublishResult
{
    public List<SendMessageBatchRequestEntry> SuccessfulMessages { get; set; } = new();
    public List<SendMessageBatchRequestEntry> FailedMessages { get; set; } = new();
    public List<BatchResultErrorEntry> SqsFailures { get; set; } = new();
    public bool CircuitBreakerTripped { get; set; }
    public Exception? Exception { get; set; }
}