using System.Reflection;
using System.Text.Json;
using K4os.Compression.LZ4;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Core.ServiceMesh;

public class ServiceMeshOptions
{
    /// <summary>
    ///     Nats durable consumer configuration
    /// </summary>
    public Func<string, ConsumerConfig, NatsJSConsumeOpts, NatsJSConsumeOpts> ConfigureConsumer =
        (s, config, opts) => opts with { };

    /// <summary>
    ///     Nats stream configuration
    /// </summary>
    public Action<string, StreamConfig> ConfigureStream = (s, config) => { };

    /// <summary>
    ///     Prefix for streams and subjects
    ///     default: null
    /// </summary>
    public string? Prefix = null;

    public string DefaultStream { get; set; } = "default";

    public int NatsPoolSize { get; set; } = 1;

    public Func<NatsOpts, NatsOpts> ConfigureNats { get; set; } = opts => opts with
    {
        Url = "nats://localhost:4222"
    };

    /// <summary>
    ///     Controls how service interface are registered in the service collection
    ///     default: auto
    /// </summary>
    public ServiceInterfaceMode InterfaceMode { get; set; } = ServiceInterfaceMode.Auto;

    /// <summary>
    ///   When enabled, does not register consumers
    /// </summary>
    public bool DeveloperMode { get; set; }

    /// <summary>
    ///     Assemblies to scan for services and consumers
    ///     default: Assembly.GetEntryAssembly()
    /// </summary>
    public Assembly[] Assemblies { get; set; } = [Assembly.GetEntryAssembly()!];

    /// <summary>
    ///     Serialize messages
    ///     default: lz4(json())
    /// </summary>
    public Func<object, bool, byte[]> Serialize { get; set; } =
        (msg, compress) =>
            compress
                ? LZ4Pickler.Pickle(JsonSerializer.SerializeToUtf8Bytes(msg))
                : JsonSerializer.SerializeToUtf8Bytes(msg);

    /// <summary>
    ///     Deserialize messages
    ///     default: !json(!lz4())
    /// </summary>
    public Func<byte[], Type, bool, object?> Deserialize { get; set; } =
        (body, type, compress) => JsonSerializer.Deserialize(compress ? LZ4Pickler.Unpickle(body) : body, type);

    /// <summary>
    ///     Nats subject for message
    /// </summary>
    public Func<Type, string> ResolveSubject { get; set; } = type => type.FullName ?? type.Name;

    /// <summary>
    ///     Resolve dotnet type from name
    ///     some potentially dangerous types could be excluded here
    ///     default: Type.GetType (unfiltered)
    /// </summary>
    public Func<string, Type?> ResolveType { get; set; } = Type.GetType;

    /// <summary>
    ///     Concurrent worker count for durable consumers
    ///     default:  max(1, cpu/2)
    /// </summary>
    public int StreamWorkers { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);

    /// <summary>
    ///     Concurrent worker count for transient consumers
    ///     default:  max(1, cpu)
    /// </summary>
    public int BroadcastWorkers { get; set; } = Math.Max(1, Environment.ProcessorCount);

    /// <summary>
    ///     Concurrent worker count for services
    ///     default:  max(1, cpu)
    /// </summary>
    public int ServiceWorkers { get; set; } = Math.Max(1, Environment.ProcessorCount);

    public Action<WebApplication, Type, Delegate> MapHttpPublishRoute { get; set; } =
        (app, type, handler) =>
        {
            app.MapPost("/api/publish/" + type.Name, handler)
                .WithTags("publish");
        };

    public Action<WebApplication, Type, Delegate> MapHttpSendRoute { get; set; } =
        (app, type, handler) =>
        {
            app.MapPost("/api/send/" + type.Name, handler)
                .WithTags("send");
        };

    public Action<WebApplication, Type, Type?, string, MethodInfo, Delegate> MapHttpRequestRoute { get; set; } =
        (app, requestType, responseType, service, method, handler) =>
        {
            app.MapPost("/api/" + service + "/" + method.Name, handler)
                .Produces(200, responseType)
                .WithTags(service);
        };
}