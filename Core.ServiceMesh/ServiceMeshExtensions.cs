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

        builder.Services.AddNats(options.NatsPoolSize, opts => options.ConfigureNats(opts));

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<ServiceMeshWorker>();
        builder.Services.AddSingleton<IServiceMesh, ServiceMeshWorker>(sp =>
            sp.GetRequiredService<ServiceMeshWorker>());
        builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ServiceMeshWorker>());

        var asms = options.Assemblies.Distinct().ToList();

        var interfaces = new List<Type>();

        var applyPrefix = (string? a) =>
        {
            if (a == null)
                return null;

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

        if (options.InterfaceMode != ServiceInterfaceMode.None)
        {
            foreach (var serviceInterface in interfaces)
            {
                var impl = Services.FirstOrDefault(x => x.InterfaceType == serviceInterface);

                if (impl == null)
                {
                    builder.Services.AddSingleton(serviceInterface,
                        DispatchProxyAsync.Create(serviceInterface, typeof(RemoteDispatchProxy)));
                }
                else
                {
                    if (options.InterfaceMode == ServiceInterfaceMode.ForceRemote)
                        builder.Services.AddSingleton(serviceInterface,
                            DispatchProxyAsync.Create(serviceInterface, typeof(RemoteDispatchProxy)));
                    else if (options.InterfaceMode == ServiceInterfaceMode.AutoTrace)
                    {
                        builder.Services.AddSingleton(serviceInterface, (sp) =>
                        {
                            var proxy = DispatchProxyAsync.Create(serviceInterface, typeof(TraceDispatchProxy));

                            if (proxy is TraceDispatchProxy traceProxy)
                            {
                                traceProxy.ServiceProvider = sp;
                                traceProxy.ImplementationType = impl.ImplementationType;
                            }

                            return proxy;
                        });
                    }
                    else
                        builder.Services.Add(new ServiceDescriptor(serviceInterface, impl.ImplementationType,
                            ServiceLifetime.Scoped));
                }
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
                Stream = applyPrefix(durableAttribute?.Stream ?? string.Empty),
                QueueGroup = applyPrefix(transientAttribute?.QueueGroup),
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