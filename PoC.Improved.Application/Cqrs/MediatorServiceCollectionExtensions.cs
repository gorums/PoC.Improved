using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace PoC.Improved.Application.Cqrs;

public sealed class MediatorConfiguration
{
    internal HashSet<Assembly> Assemblies { get; } = new();
    internal List<Type> Behaviors { get; } = new();

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
}

public static class MediatorServiceCollectionExtensions
{
    /// <summary>
    /// Registers the custom IMediator, scans assemblies for IRequestHandler implementations,
    /// and registers pipeline behaviors as open generics in the order they were added.
    /// </summary>
    public static IServiceCollection AddMediator(
        this IServiceCollection services,
        Action<MediatorConfiguration> configure)
    {
        var config = new MediatorConfiguration();
        configure(config);

        services.AddScoped<IMediator, Mediator>();

        foreach (var assembly in config.Assemblies)
            RegisterHandlersFromAssembly(services, assembly);

        foreach (var behaviorType in config.Behaviors)
            services.AddTransient(typeof(IPipelineBehavior<,>), behaviorType);

        return services;
    }

    private static void RegisterHandlersFromAssembly(IServiceCollection services, Assembly assembly)
    {
        var openHandler = typeof(IRequestHandler<,>);

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                continue;

            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == openHandler)
                    services.AddTransient(iface, type);
            }
        }
    }
}
