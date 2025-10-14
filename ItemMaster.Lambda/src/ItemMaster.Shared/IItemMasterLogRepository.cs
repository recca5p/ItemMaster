namespace ItemMaster.Shared;

public interface IItemMasterLogRepository
{
    Task<Result> LogProcessingResultAsync(string operation, bool success, RequestSource requestSource,
        string? errorMessage = null, int? itemCount = null, string? traceId = null,
        CancellationToken cancellationToken = default);
}