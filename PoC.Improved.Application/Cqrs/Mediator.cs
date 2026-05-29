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
    private static readonly ConcurrentDictionary<Type, RequestHandlerWrapperBase> _sendWrappers = new();
    private static readonly ConcurrentDictionary<Type, RequestHandlerWrapperBase> _streamWrappers = new();
    private readonly IServiceProvider _serviceProvider;

    public Mediator(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (RequestHandlerWrapper<TResponse>)_sendWrappers.GetOrAdd(
            request.GetType(),
            static reqType =>
            {
                var wrapperType = typeof(RequestHandlerWrapperImpl<,>)
                    .MakeGenericType(reqType, GetResponseType(reqType, typeof(IRequest<>)));
                return (RequestHandlerWrapperBase)Activator.CreateInstance(wrapperType)!;
            });

        return wrapper.Handle(request, _serviceProvider, cancellationToken);
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (StreamRequestHandlerWrapper<TResponse>)_streamWrappers.GetOrAdd(
            request.GetType(),
            static reqType =>
            {
                var wrapperType = typeof(StreamRequestHandlerWrapperImpl<,>)
                    .MakeGenericType(reqType, GetResponseType(reqType, typeof(IStreamRequest<>)));
                return (RequestHandlerWrapperBase)Activator.CreateInstance(wrapperType)!;
            });

        return wrapper.Handle(request, _serviceProvider, cancellationToken);
    }

    private static Type GetResponseType(Type requestType, Type openMarker)
    {
        var iface = requestType.GetInterfaces()
            .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == openMarker);
        return iface.GetGenericArguments()[0];
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

        for (var i = behaviors.Count - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var inner = next;
            next = () => behavior.Handle(typedRequest, inner, cancellationToken);
        }

        return next();
    }
}

internal abstract class StreamRequestHandlerWrapper<TResponse> : RequestHandlerWrapperBase
{
    public abstract IAsyncEnumerable<TResponse> Handle(
        object request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

internal sealed class StreamRequestHandlerWrapperImpl<TRequest, TResponse> : StreamRequestHandlerWrapper<TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public override IAsyncEnumerable<TResponse> Handle(
        object request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;
        var handler = serviceProvider.GetRequiredService<IStreamRequestHandler<TRequest, TResponse>>();
        var behaviors = serviceProvider.GetServices<IStreamPipelineBehavior<TRequest, TResponse>>().ToList();

        StreamHandlerDelegate<TResponse> next = () => handler.Handle(typedRequest, cancellationToken);

        for (var i = behaviors.Count - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var inner = next;
            next = () => behavior.Handle(typedRequest, inner, cancellationToken);
        }

        return next();
    }
}
