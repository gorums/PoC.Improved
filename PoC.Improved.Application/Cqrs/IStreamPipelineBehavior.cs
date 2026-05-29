namespace PoC.Improved.Application.Cqrs;

/// <summary>
/// Continuation for stream pipeline behaviors. Returning IAsyncEnumerable instead of Task
/// lets the behavior intercept individual items as they flow through.
/// </summary>
public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<TResponse>();

/// <summary>
/// Cross-cutting concern wrapping every stream handler invocation. First-registered = outermost.
/// </summary>
public interface IStreamPipelineBehavior<in TRequest, TResponse>
    where TRequest : notnull
{
    IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
