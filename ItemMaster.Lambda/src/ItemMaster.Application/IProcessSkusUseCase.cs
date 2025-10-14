using ItemMaster.Contracts;
using ItemMaster.Shared;

namespace ItemMaster.Application;

public interface IProcessSkusUseCase
{
    Task<Result<ProcessSkusResponse>> ExecuteAsync(ProcessSkusRequest request,
        RequestSource requestSource = RequestSource.Unknown,
        string? traceId = null,
        CancellationToken cancellationToken = default);
}