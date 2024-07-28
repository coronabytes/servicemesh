using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text.Json;
using Core.ServiceMesh.Abstractions;
using K4os.Compression.LZ4;

namespace Core.ServiceMesh;

public class ServiceMeshOptions
{
    /// <summary>
    ///     prefix for streams and subjects
    ///     default: null
    /// </summary>
    public string? Prefix = null;

 /// <summary>
    ///     nats connection string
    ///     default: nats://localhost:4222
    /// </summary>
    public string Nats { get; set; } = "nats://localhost:4222";

    /// <summary>
    ///     controls how service interface are registered in the service collection
    ///     default: auto
    /// </summary>
    public ServiceRegisterMode RegisterServiceInterface { get; set; } = ServiceRegisterMode.Auto;

    /// <summary>
    ///     assemblies to scan for services and consumers
    ///     default: Assembly.GetEntryAssembly()
    /// </summary>
    public Assembly[] Assemblies { get; set; } = [Assembly.GetEntryAssembly()!];

    /// <summary>
    ///     serialize messages
    ///     default: lz4(json())
    /// </summary>
    public Func<object, bool, byte[]> Serialize { get; set; } =
        (msg, compress) =>
            compress
                ? LZ4Pickler.Pickle(JsonSerializer.SerializeToUtf8Bytes(msg))
                : JsonSerializer.SerializeToUtf8Bytes(msg);

    /// <summary>
    ///     deserialize messages
    ///     default: !json(!lz4())
    /// </summary>
    public Func<byte[], Type, bool, object?> Deserialize { get; set; } =
        (body, type, compress) => JsonSerializer.Deserialize(compress ? LZ4Pickler.Unpickle(body) : body, type);

    /// <summary>
    ///     nats subject name from service mesh attribute + method info
    /// </summary>
    public Func<ServiceMeshAttribute, MethodInfo, string> ResolveService { get; set; } = (attr, info) =>
        $"{attr.Name}.{info.Name}.G{info.GetGenericArguments().Length}P{info.GetParameters().Length}";

    /// <summary>
    ///   nats subject for message
    /// </summary>
    public Func<Type, string> ResolveSubject { get; set; } = type => type.Name;

    /// <summary>
    ///     resolve dotnet type from name
    ///     some potentially dangerous types could be excluded here
    ///     default: Type.GetType (unfiltered)
    /// </summary>
    public Func<string, Type?> ResolveType { get; set; } = Type.GetType;

    /// <summary>
    ///     concurrent worker count for durable consumers
    ///     default:  max(1, cpu/2)
    /// </summary>
    public int StreamWorkers { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);

    /// <summary>
    ///     concurrent worker count for transient consumers
    ///     default:  max(1, cpu)
    /// </summary>
    public int BroadcastWorkers { get; set; } = Math.Max(1, Environment.ProcessorCount);

    /// <summary>
    ///     concurrent worker count for services
    ///     default:  max(1, cpu)
    /// </summary>
    public int ServiceWorkers { get; set; } = Math.Max(1, Environment.ProcessorCount);
}