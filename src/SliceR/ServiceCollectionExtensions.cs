using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SliceR.Authorization;
using SliceR.Validation;

namespace SliceR;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSliceR(this IServiceCollection services,
        Assembly? assembly = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        bool includeInternalTypes = true)
    {
        assembly ??= Assembly.GetCallingAssembly();

        services.AddMediatR(c =>
        {
            c.AutoRegisterRequestProcessors = true;
            c.RegisterServicesFromAssembly(assembly);
        });

        return services
                 .AddValidationSlice(assembly, lifetime, includeInternalTypes)
                 .AddAuthorizationSlice()
                 .AddResourceResolvers(assembly, lifetime, includeInternalTypes);
    }

    private static IServiceCollection AddValidationSlice(this IServiceCollection services,
        Assembly? assembly = null,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        bool includeInternalTypes = true)
    {
        assembly ??= Assembly.GetEntryAssembly();

        //:: Fluent Validation
        AssemblyScanner
            .FindValidatorsInAssembly(assembly, includeInternalTypes)
            .ForEach(r =>
            {
                services.TryAddEnumerable(new ServiceDescriptor(r.InterfaceType, r.ValidatorType, lifetime));
                services.TryAdd(new ServiceDescriptor(r.ValidatorType, r.ValidatorType, lifetime));
            });

        //:: MediatR Bridge
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        //:: ASP.NET Core Controllers
        services.AddControllers(o =>
        {
            o.Filters.Add<ValidationExceptionFilter>();
        });

        return services;
    }

    private static IServiceCollection AddAuthorizationSlice(this IServiceCollection services)
    {
        //:: ASP.NET Core Authorization
        services.AddHttpContextAccessor();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuthorizationMiddlewareResultHandler>();

        //:: MediatR Bridge
        services.AddScoped<IAuthorizationProvider, AuthorizationProvider>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));

        //:: ASP.NET Core Controllers
        services.AddControllers(o =>
        {
            o.Filters.Add<AuthorizationExceptionFilter>();
        });

        return services;
    }

    private static IServiceCollection AddResourceResolvers(this IServiceCollection services,
        Assembly assembly,
        ServiceLifetime lifetime,
        bool includeInternalTypes)
    {
        var bindingFlags = BindingFlags.Public | (includeInternalTypes ? BindingFlags.NonPublic : 0);

        var resolverTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition)
            .Where(t => t.GetInterfaces().Any(i =>
                i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(IResourceResolver<,>)))
            .ToList();

        var registryMappings = new Dictionary<Type, Type>();

        foreach (var resolverType in resolverTypes)
        {
            var interfaces = resolverType.GetInterfaces()
                .Where(i => i.IsGenericType &&
                           i.GetGenericTypeDefinition() == typeof(IResourceResolver<,>));

            foreach (var @interface in interfaces)
            {
                services.TryAdd(new ServiceDescriptor(@interface, resolverType, lifetime));
                services.TryAdd(new ServiceDescriptor(resolverType, resolverType, lifetime));

                // Extract TRequest type for the registry
                var requestType = @interface.GetGenericArguments()[0];

                // Only register the first resolver found for each request type
                // This prevents issues in test environments where multiple resolvers might exist
                if (!registryMappings.ContainsKey(requestType))
                {
                    registryMappings[requestType] = resolverType;
                }
            }
        }

        // Register the resolver registry as a singleton
        var registry = new ResolverRegistry(registryMappings);
        services.AddSingleton<IResolverRegistry>(registry);

        return services;
    }

    public static IServiceCollection WithResourceResolvers(this IServiceCollection services) => services;

    public static IServiceCollection WithResourceResolver<TRequest, TResource>(
        this IServiceCollection services,
        Func<TRequest, IServiceProvider, CancellationToken, Task<TResource?>> resolver)
    {
        services.AddTransient<IResourceResolver<TRequest, TResource>>(serviceProvider =>
            new DelegateResourceResolver<TRequest, TResource>(resolver, serviceProvider));

        return services;
    }
}
