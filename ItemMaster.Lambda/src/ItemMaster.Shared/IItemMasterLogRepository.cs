namespace ItemMaster.Shared;

public interface IItemMasterLogRepository
{
    Task<Result> LogItemSourceAsync(ItemMasterSourceLog log, CancellationToken cancellationToken = default);
}