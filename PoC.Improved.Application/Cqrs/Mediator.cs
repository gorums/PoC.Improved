using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace PoC.Improved.Application.Cqrs;

/// <summary>
/// Default IMediator implementation. Dispatch is done via a per-request-type
/// wrapper cache - the wrapper knows TRequest at compile time, so the rest of the
/// pipeline (handler resolution + behaviors) stays type-safe and reflection-free.
/// </summary>
public sealed class Mediator : IMediator
{
    private static readonly ConcurrentDictionary<Type, RequestHandlerWrapperBase> _wrappers = new();
    private readonly IServiceProvider _serviceProvider;

    public Mediator(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (RequestHandlerWrapper<TResponse>)_wrappers.GetOrAdd(
            request.GetType(),
            static reqType =>
            {
                var wrapperType = typeof(RequestHandlerWrapperImpl<,>)
                    .MakeGenericType(reqType, GetResponseType(reqType));
                return (RequestHandlerWrapperBase)Activator.CreateInstance(wrapperType)!;
            });

        return wrapper.Handle(request, _serviceProvider, cancellationToken);
    }

    private static Type GetResponseType(Type requestType)
    {
        var requestInterface = requestType.GetInterfaces()
            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));
        return requestInterface.GetGenericArguments()[0];
    }
}

internal abstract class RequestHandlerWrapperBase
{
}

internal abstract class RequestHandlerWrapper<TResponse> : RequestHandlerWrapperBase
{
    public abstract Task<TResponse> Handle(
        object request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

internal sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override Task<TResponse> Handle(
        object request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;
        var handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToList();

        RequestHandlerDelegate<TResponse> next = () => handler.Handle(typedRequest, cancellationToken);

        // Wrap in reverse so the first-registered behavior is outermost.
        for (var i = behaviors.Count - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var inner = next;
            next = () => behavior.Handle(typedRequest, inner, cancellationToken);
        }

        return next();
    }
}
