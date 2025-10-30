namespace ItemMaster.Shared;

public interface IItemMasterLogRepository
{
    Task<Result> LogItemSourceAsync(ItemMasterSourceLog log, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the latest log entries for the given SKUs as successfully sent to SQS.
    /// </summary>
    Task<Result> MarkSentToSqsAsync(IEnumerable<string> skus, CancellationToken cancellationToken = default);
}