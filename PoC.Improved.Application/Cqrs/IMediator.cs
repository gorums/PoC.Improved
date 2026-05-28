namespace PoC.Improved.Application.Cqrs;

/// <summary>
/// Dispatches a request through the registered pipeline behaviors to its handler.
/// Mirrors the subset of MediatR's IMediator that we use - just Send.
/// </summary>
public interface IMediator
{
    Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default);
}
