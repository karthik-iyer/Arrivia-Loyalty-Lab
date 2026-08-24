using LoyaltyLab.Domain.Common;

namespace LoyaltyLab.Application.Abstractions;

/// <summary>
/// One application operation. Endpoints and tests call this; adapters stay behind ports.
/// </summary>
public interface IUseCase<TRequest, TResponse>
{
    Task<Result<TResponse>> ExecuteAsync(TRequest request, CancellationToken cancellationToken);
}
