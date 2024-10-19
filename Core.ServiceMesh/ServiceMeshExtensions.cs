using System.Collections.Frozen;
using System.Reflection;
using Core.ServiceMesh.Abstractions;
using Core.ServiceMesh.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NATS.Client.Hosting;
using OpenTelemetry.Trace;

namespace Core.ServiceMesh;

public static class ServiceMeshExtensions
{
    internal static readonly List<ConsumerRegistration> Consumers = new();
    internal static readonly List<ServiceRegistration> Services = new();

    public static IServiceCollection AddServiceMesh(this IServiceCollection services,
        Action<ServiceMeshOptions> configure)
    {
        var options = new ServiceMeshOptions();
        configure(options);

        services.AddNats(options.NatsPoolSize, opts => options.ConfigureNats(opts));

        services.AddSingleton(options);
        services.AddSingleton<ServiceMeshWorker>();
        services.AddSingleton<IServiceMesh, ServiceMeshWorker>(sp =>
            sp.GetRequiredService<ServiceMeshWorker>());
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ServiceMeshWorker>());

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

        foreach (var type in asms.SelectMany(asm =>
                     asm.GetTypes().Where(y =>
                     {
                         return y.GetCustomAttribute<ServiceMeshAttribute>() != null
                                || y.GetInterfaces().Any(z => z.GetCustomAttribute<ServiceMeshAttribute>() != null);
                     })))
            if (type.IsInterface)
            {
                interfaces.Add(type);
            }
            else
            {
                var itype = type.GetInterfaces().Single();
                var attr = itype.GetCustomAttribute<ServiceMeshAttribute>();

                if (type.Name.EndsWith("RemoteProxy") || type.Name.EndsWith("TraceProxy"))
                    continue;

                var methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
                var dic = new Dictionary<string, MethodInfo>();

                foreach (var method in methods)
                {
                    var subject =
                        applyPrefix(
                            $"{attr!.Name}.{method.Name}.G{method.GetGenericArguments().Length}P{method.GetParameters().Length}");

                    if (subject == null)
                        continue;

                    dic.Add(subject, method);
                }

                Services.Add(new ServiceRegistration
                {
                    Name = attr!.Name,
                    Sub = applyPrefix(attr.Name)!,
                    InterfaceType = itype,
                    ImplementationType = type,
                    Methods = dic.ToFrozenDictionary(),
                    QueueGroup = applyPrefix(attr.Name)!
                });

                services.Add(new ServiceDescriptor(type, type, ServiceLifetime.Scoped));
            }

        if (options.InterfaceMode != ServiceInterfaceMode.None)
            foreach (var serviceInterface in interfaces)
            {
                var impl = Services.FirstOrDefault(x => x.InterfaceType == serviceInterface);

                if (impl == null)
                {
                    var remoteProxy = serviceInterface.Assembly.GetType(serviceInterface.FullName + "RemoteProxy");

                    if (remoteProxy != null)
                        services.AddSingleton(serviceInterface, remoteProxy);
                }
                else
                {
                    if (options.InterfaceMode == ServiceInterfaceMode.ForceRemote)
                    {
                        var remoteProxy = serviceInterface.Assembly.GetType(serviceInterface.FullName + "RemoteProxy");

                        if (remoteProxy != null)
                            services.AddSingleton(serviceInterface, remoteProxy);
                    }
                    else if (options.InterfaceMode == ServiceInterfaceMode.AutoTrace)
                    {
                        var traceProxy =
                            serviceInterface.Assembly.GetType(impl.ImplementationType.FullName + "TraceProxy");

                        if (traceProxy != null)
                            services.AddSingleton(serviceInterface, traceProxy);
                    }
                    else
                    {
                        services.Add(new ServiceDescriptor(serviceInterface, impl.ImplementationType,
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

            if (ifaces.Count > 1) ifaces.ToArray();

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
                services.Add(new ServiceDescriptor(consumer, consumer, ServiceLifetime.Scoped));

            Consumers.Add(new ConsumerRegistration
            {
                IsDurable = durableAttribute != null,
                Durable = durableAttribute,
                Name = applyPrefix(durableAttribute?.Name ?? string.Empty)!,
                Subjects = map.Select(x => applyPrefix(options.ResolveSubject(x.Key))!).ToArray(),
                Stream = applyPrefix(durableAttribute?.Stream ?? options.DefaultStream)!,
                QueueGroup = applyPrefix(transientAttribute?.QueueGroup),
                Consumer = consumer,
                Obsolete = obsolete,
                Methods = map.ToFrozenDictionary(x => applyPrefix(options.ResolveSubject(x.Key))!,
                    x => (x.Value, x.Key))
            });
        }

        return services;
    }

    public static TracerProviderBuilder AddServiceMeshInstrumentation(this TracerProviderBuilder builder)
    {
        builder.AddSource("core.servicemesh");
        return builder;
    }

    /*internal static void DynamicPublish<T>(WebApplication app) where T : class
    {
        var options = app.Services.GetRequiredService<ServiceMeshOptions>();

        options.MapHttpPublishRoute(app, typeof(T),
            async ([FromBody] T value, [FromServices] IServiceMesh mesh) => { await mesh.PublishAsync(value); });
    }

    internal static void DynamicSend<T>(WebApplication app) where T : class
    {
        var options = app.Services.GetRequiredService<ServiceMeshOptions>();

        options.MapHttpSendRoute(app, typeof(T),
            async ([FromBody] T value, [FromServices] IServiceMesh mesh) => { await mesh.SendAsync(value); });
    }

    internal static void DynamicRequestT<TReq, TRet>(WebApplication app, string service, MethodInfo info)
        where TReq : class
    {
        var options = app.Services.GetRequiredService<ServiceMeshOptions>();

        options.MapHttpRequestRoute(app, typeof(TReq), typeof(TRet), service, info,
            async ([FromBody] TReq value, [FromServices] IServiceMesh mesh) =>
            await mesh.RequestAsync<TRet>(info, [value]));
    }

    internal static void DynamicRequest<TReq>(WebApplication app, string service, MethodInfo info) where TReq : class
    {
        var options = app.Services.GetRequiredService<ServiceMeshOptions>();

        options.MapHttpRequestRoute(app, typeof(TReq), null, service, info,
            async ([FromBody] TReq value, [FromServices] IServiceMesh mesh) =>
            await mesh.RequestAsync(info, [value]));
    }*/

    public static WebApplication MapServiceMesh(this WebApplication app, string[]? consumerSuffixes = null)
    {
        /*var flags = BindingFlags.NonPublic | BindingFlags.Static;

        var dynPublish = typeof(ServiceMeshExtensions).GetMethod(nameof(DynamicPublish), flags);
        var dynSend = typeof(ServiceMeshExtensions).GetMethod(nameof(DynamicSend), flags);
        var dynRequest = typeof(ServiceMeshExtensions).GetMethod(nameof(DynamicRequest), flags);
        var dynRequestT = typeof(ServiceMeshExtensions).GetMethod(nameof(DynamicRequestT), flags);

        var options = app.Services.GetRequiredService<ServiceMeshOptions>();
        var asms = options.Assemblies.Distinct().ToList();

        if (consumerSuffixes != null)
        {
            var messageTypes = asms.SelectMany(x =>
                x.GetTypes().Where(y => y.IsPublic && consumerSuffixes.Any(z => y.Name.EndsWith(z)))).ToList();

            foreach (var msgType in messageTypes)
            {
                dynPublish!.MakeGenericMethod(msgType).Invoke(null, [app]);
                dynSend!.MakeGenericMethod(msgType).Invoke(null, [app]);
            }
        }

        foreach (var service in Services)
        foreach (var method in service.Methods)
        {
            // no generics over http
            if (method.Value.GetParameters().Length != 1
                || method.Value.GetGenericArguments().Length > 0)
                continue;

            var retType = method.Value.ReturnType;
            var reqType = method.Value.GetParameters()[0].ParameterType;

            if (retType.GenericTypeArguments.Length == 0)
            {
                dynRequest!.MakeGenericMethod(reqType).Invoke(null, [app, service.Name, method.Value]);
            }
            else
            {
                var resType = retType.GenericTypeArguments[0];
                dynRequestT!.MakeGenericMethod(reqType, resType).Invoke(null, [app, service.Name, method.Value]);
            }
        }*/

        return app;
    }
}