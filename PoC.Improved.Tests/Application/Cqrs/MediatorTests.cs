using Microsoft.Extensions.DependencyInjection;
using PoC.Improved.Application.Cqrs;

namespace PoC.Improved.Tests.Application.Cqrs;

public sealed record PingQuery(string Value) : IRequest<string>;

public sealed class PingHandler : IRequestHandler<PingQuery, string>
{
    public Task<string> Handle(PingQuery request, CancellationToken cancellationToken)
        => Task.FromResult($"pong:{request.Value}");
}

public abstract class TagBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public static readonly List<string> Calls = new();
    private readonly string _tag;

    protected TagBehavior(string tag) => _tag = tag;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        Calls.Add($"{_tag}:before");
        var response = await next();
        Calls.Add($"{_tag}:after");
        return response;
    }
}

public sealed class OuterBehavior<TRequest, TResponse> : TagBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public OuterBehavior() : base("outer") { }
}

public sealed class InnerBehavior<TRequest, TResponse> : TagBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public InnerBehavior() : base("inner") { }
}

public class MediatorTests
{
    [Fact]
    public async Task Send_routes_to_registered_handler_and_returns_result()
    {
        var sp = BuildServices(behaviors: Array.Empty<Type>());
        var mediator = sp.GetRequiredService<IMediator>();

        var result = await mediator.Send(new PingQuery("hi"));

        Assert.Equal("pong:hi", result);
    }

    [Fact]
    public async Task ISender_resolves_to_the_same_instance_as_IMediator()
    {
        var sp = BuildServices(behaviors: Array.Empty<Type>());
        using var scope = sp.CreateScope();

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        Assert.Same(mediator, sender);

        var result = await sender.Send(new PingQuery("from-sender"));
        Assert.Equal("pong:from-sender", result);
    }

    [Fact]
    public async Task Send_throws_when_handler_is_not_registered()
    {
        var services = new ServiceCollection();
        services.AddScoped<IMediator, Mediator>();
        // No handler registered.
        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.Send(new PingQuery("x")));
    }

    [Fact]
    public async Task Send_invokes_pipeline_behaviors_in_registration_order_outer_first()
    {
        TagBehavior<PingQuery, string>.Calls.Clear();
        var sp = BuildServices(behaviors: new[]
        {
            typeof(OuterBehavior<,>),
            typeof(InnerBehavior<,>),
        });
        var mediator = sp.GetRequiredService<IMediator>();

        await mediator.Send(new PingQuery("x"));

        Assert.Equal(
            new[] { "outer:before", "inner:before", "inner:after", "outer:after" },
            TagBehavior<PingQuery, string>.Calls);
    }

    [Fact]
    public void AddMediator_throws_when_behavior_is_not_open_generic()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddMediator(cfg => cfg.AddOpenBehavior(typeof(string))));
    }

    private static IServiceProvider BuildServices(IReadOnlyList<Type> behaviors)
    {
        var services = new ServiceCollection();
        services.AddMediator(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<MediatorTests>();
            foreach (var b in behaviors)
                cfg.AddOpenBehavior(b);
        });
        return services.BuildServiceProvider();
    }
}
