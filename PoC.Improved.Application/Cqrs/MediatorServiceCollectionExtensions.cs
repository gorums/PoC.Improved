using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace PoC.Improved.Application.Cqrs;

public sealed class MediatorConfiguration
{
    internal HashSet<Assembly> Assemblies { get; } = new();
    internal List<Type> Behaviors { get; } = new();
    internal List<Type> StreamBehaviors { get; } = new();

    public MediatorConfiguration RegisterServicesFromAssemblyContaining<T>()
    {
        Assemblies.Add(typeof(T).Assembly);
        return this;
    }

    public MediatorConfiguration RegisterServicesFromAssembly(Assembly assembly)
    {
        Assemblies.Add(assembly);
        return this;
    }

    public MediatorConfiguration AddOpenBehavior(Type behaviorType)
    {
        if (!behaviorType.IsGenericTypeDefinition)
            throw new ArgumentException(
                "Behavior must be an open generic type (e.g. typeof(MyBehavior<,>)).",
                nameof(behaviorType));

        Behaviors.Add(behaviorType);
        return this;
    }

    public MediatorConfiguration AddOpenStreamBehavior(Type behaviorType)
    {
        if (!behaviorType.IsGenericTypeDefinition)
            throw new ArgumentException(
                "Stream behavior must be an open generic type (e.g. typeof(MyStreamBehavior<,>)).",
                nameof(behaviorType));

        StreamBehaviors.Add(behaviorType);
        return this;
    }
}

public static class MediatorServiceCollectionExtensions
{
    /// <summary>
    /// Registers the custom IMediator/ISender, scans assemblies for IRequestHandler and
    /// IStreamRequestHandler implementations, and registers pipeline behaviors (and stream
    /// pipeline behaviors) as open generics in the order they were added.
    /// </summary>
    public static IServiceCollection AddMediator(
        this IServiceCollection services,
        Action<MediatorConfiguration> configure)
    {
        var config = new MediatorConfiguration();
        configure(config);

        services.AddScoped<IMediator, Mediator>();
        services.AddScoped<ISender>(sp => sp.GetRequiredService<IMediator>());

        foreach (var assembly in config.Assemblies)
            RegisterHandlersFromAssembly(services, assembly);

        foreach (var behaviorType in config.Behaviors)
            services.AddTransient(typeof(IPipelineBehavior<,>), behaviorType);

        foreach (var behaviorType in config.StreamBehaviors)
            services.AddTransient(typeof(IStreamPipelineBehavior<,>), behaviorType);

        return services;
    }

    private static void RegisterHandlersFromAssembly(IServiceCollection services, Assembly assembly)
    {
        var openRequest = typeof(IRequestHandler<,>);
        var openStream = typeof(IStreamRequestHandler<,>);

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                continue;

            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType) continue;
                var def = iface.GetGenericTypeDefinition();
                if (def == openRequest || def == openStream)
                    services.AddTransient(iface, type);
            }
        }
    }
}
