using ItemMaster.Contracts;

namespace ItemMaster.Application;

public interface IProcessSkusUseCase
{
    Task<ProcessSkusResponse> ExecuteAsync(IEnumerable<string> skus, string source, string requestId, CancellationToken ct = default);
}

