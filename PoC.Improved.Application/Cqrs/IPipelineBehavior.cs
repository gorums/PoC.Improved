namespace PoC.Improved.Application.Cqrs;

/// <summary>
/// Continuation delegate used by pipeline behaviors to invoke the next stage of the pipeline
/// (next behavior, or the handler if no more behaviors remain).
/// </summary>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// Cross-cutting concern that wraps every handler invocation in the pipeline.
/// First-registered behavior is outermost (closest to the caller).
/// </summary>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : notnull
{
    Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
