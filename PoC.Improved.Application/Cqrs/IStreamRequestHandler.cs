namespace PoC.Improved.Application.Cqrs;

/// <summary>
/// Handles a stream request, returning items as they're produced.
/// Use `yield return` inside Handle to push items lazily.
/// </summary>
public interface IStreamRequestHandler<in TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
