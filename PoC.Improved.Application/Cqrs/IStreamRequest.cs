namespace PoC.Improved.Application.Cqrs;

/// <summary>
/// Marker for a request that produces a stream of <typeparamref name="TResponse"/> items.
/// The handler returns IAsyncEnumerable so items flow as they're produced.
/// </summary>
public interface IStreamRequest<out TResponse>
{
}
