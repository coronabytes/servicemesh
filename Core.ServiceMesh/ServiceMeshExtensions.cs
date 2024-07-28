using System.Reflection;
using Core.ServiceMesh.Abstractions;
using Core.ServiceMesh.Internal;
using Core.ServiceMesh.Proxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NATS.Client.Hosting;

namespace Core.ServiceMesh;

public static class ServiceMeshExtensions
{
    internal static readonly List<ConsumerRegistration> Consumers = new();
    internal static readonly List<ServiceRegistration> Services = new();

    public static IHostApplicationBuilder AddServiceMesh(this IHostApplicationBuilder builder,
        Action<ServiceMeshOptions> configure)
    {
        var options = new ServiceMeshOptions();
        configure(options);

        builder.Services.AddNats(1, opts => opts with
        {
            Url = options.Nats
        });

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<ServiceMeshWorker>();
        builder.Services.AddSingleton<IServiceMesh, ServiceMeshWorker>(sp =>
            sp.GetRequiredService<ServiceMeshWorker>());
        builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ServiceMeshWorker>());

        var asms = options.Assemblies.Distinct().ToList();

        var interfaces = new List<Type>();

        var applyPrefix = (string a) =>
        {
            if (options.Prefix != null)
                return $"{options.Prefix}-{a}";
            return a;
        };

        foreach (var type in asms.SelectMany(asm=> asm.GetTypes().Where(y => y.GetCustomAttribute<ServiceMeshAttribute>() != null)))
        {
            var attr = type.GetCustomAttribute<ServiceMeshAttribute>()!;

            if (type.IsInterface)
            {
                interfaces.Add(type);
            }
            else
            {
                var itype = type.GetInterfaces().Single();
                var methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);

                foreach (var method in methods)
                {
                    Services.Add(new ServiceRegistration
                    {
                        Subject = applyPrefix(options.ResolveService(attr, method)),
                        InterfaceType = itype,
                        ImplementationType = type,
                        Method = method,
                        QueueGroup = applyPrefix(attr.QueueGroup ?? attr.Name)
                    });
                }

                builder.Services.Add(new ServiceDescriptor(type, type, ServiceLifetime.Scoped));
            }
        }

        if (options.RegisterServiceInterface != ServiceRegisterMode.None)
        {
            foreach (var serviceInterface in interfaces)
            {
                var impl = Services.FirstOrDefault(x => x.InterfaceType == serviceInterface);

                if (impl == null || options.RegisterServiceInterface == ServiceRegisterMode.ForceProxy)
                    builder.Services.AddSingleton(serviceInterface,
                        DispatchProxyAsync.Create(serviceInterface, typeof(MeshDispatchProxy)));
                else
                    builder.Services.Add(new ServiceDescriptor(serviceInterface, impl.ImplementationType,
                        ServiceLifetime.Scoped));
            }
        }

        foreach (var handler in asms.SelectMany(asm => asm.GetTypes().Where(x => x.GetInterfaces()
                     .Any(y => y.IsGenericType && y.GetGenericTypeDefinition() == typeof(IConsumer<>))).ToList()))
        {
            var durableAttribute = handler.GetCustomAttribute<DurableConsumerAttribute>();
            var transientAttribute = handler.GetCustomAttribute<TransientConsumerAttribute>();

            if (durableAttribute != null && transientAttribute != null)
                continue;

            if (durableAttribute == null && transientAttribute == null)
                continue;

            var itype = handler.GetInterfaces().SingleOrDefault();
            var msgType = itype.GetGenericArguments()[0];

            var obsolete = handler.GetCustomAttribute<ObsoleteAttribute>() != null;

            if (!obsolete)
                builder.Services.Add(new ServiceDescriptor(handler, handler, ServiceLifetime.Scoped));

            Consumers.Add(new ConsumerRegistration
            {
                IsDurable = durableAttribute != null,
                Name = applyPrefix(durableAttribute?.Name ?? string.Empty),
                Subject = applyPrefix(options.ResolveSubject(msgType)),
                Stream = applyPrefix(durableAttribute?.Stream ?? transientAttribute?.QueueGroup ?? string.Empty),
                Type = msgType,
                Consumer = handler,
                Obsolete = obsolete,
                Method = handler.GetMethod(nameof(IConsumer<object>
                    .ConsumeAsync))!
            });
        }

        return builder;
    }
}