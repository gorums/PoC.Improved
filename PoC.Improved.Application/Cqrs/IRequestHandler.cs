namespace PoC.Improved.Application.Cqrs;

/// <summary>
/// Handles a request of type <typeparamref name="TRequest"/> returning <typeparamref name="TResponse"/>.
/// </summary>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
