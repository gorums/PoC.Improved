namespace PoC.Improved.Application.Cqrs;

/// <summary>
/// Sends a request (one response) or starts a stream (many responses) through the pipeline.
/// Narrower than IMediator - inject this at call sites that only need to send/stream so the
/// dependency surface stays minimal. IMediator extends ISender.
/// </summary>
public interface ISender
{
    Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default);
}
