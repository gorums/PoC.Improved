namespace PoC.Improved.Application.Cqrs;

/// <summary>
/// Sends a request through the pipeline. Narrower than IMediator - inject this
/// at call sites that only need to send (e.g. endpoints, controllers) so the
/// dependency surface stays minimal. IMediator extends ISender.
/// </summary>
public interface ISender
{
    Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default);
}
