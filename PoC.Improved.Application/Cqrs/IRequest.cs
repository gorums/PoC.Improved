namespace PoC.Improved.Application.Cqrs;

/// <summary>
/// Marker interface for a request that returns a response of type <typeparamref name="TResponse"/>.
/// Names match MediatR so handler/behavior files only need to swap the namespace.
/// </summary>
public interface IRequest<out TResponse>
{
}
