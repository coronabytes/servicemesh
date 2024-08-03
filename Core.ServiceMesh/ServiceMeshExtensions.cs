using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
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

        foreach (var consumer in asms.SelectMany(asm => asm.GetTypes().Where(x => x.GetInterfaces()
                     .Any(y => y.IsGenericType && y.GetGenericTypeDefinition() == typeof(IConsumer<>))).ToList()))
        {
            var durableAttribute = consumer.GetCustomAttribute<DurableConsumerAttribute>();
            var transientAttribute = consumer.GetCustomAttribute<TransientConsumerAttribute>();

            if (durableAttribute != null && transientAttribute != null)
                continue;

            if (durableAttribute == null && transientAttribute == null)
                continue;

            var ifaces = consumer.GetInterfaces()
                .Where(y => y.IsGenericType && y.GetGenericTypeDefinition() == typeof(IConsumer<>)).ToList();

            if (ifaces.Count > 1)
            {
                ifaces.ToArray();
            }

            var map = new Dictionary<Type, MethodInfo>();

            foreach (var iface in ifaces)
            {
                var msgType = iface.GetGenericArguments()[0];

                var method = consumer.GetMethod(nameof(IConsumer<object>.ConsumeAsync), 
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy, 
                    [msgType, typeof(CancellationToken)]);

                map.Add(msgType, method!);
            }

            var obsolete = consumer.GetCustomAttribute<ObsoleteAttribute>() != null;

            if (!obsolete)
                builder.Services.Add(new ServiceDescriptor(consumer, consumer, ServiceLifetime.Scoped));

            Consumers.Add(new ConsumerRegistration
            {
                IsDurable = durableAttribute != null,
                Name = applyPrefix(durableAttribute?.Name ?? string.Empty)!,
                Subjects = map.Select(x => applyPrefix(options.ResolveSubject(x.Key))!).ToArray(),
                Stream = applyPrefix(durableAttribute?.Stream ?? string.Empty)!,
                QueueGroup = applyPrefix(transientAttribute?.QueueGroup),
                Consumer = consumer,
                Obsolete = obsolete,
                Methods = map.ToDictionary(x=> applyPrefix(options.ResolveSubject(x.Key))!, 
                    x => (x.Value, x.Key))
            });
        }

        return builder;
    }
}