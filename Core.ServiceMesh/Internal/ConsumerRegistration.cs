using System.Collections.Frozen;
using System.Reflection;
using Core.ServiceMesh.Abstractions;

namespace Core.ServiceMesh.Internal;

internal class ConsumerRegistration
{
    public string Name { get; init; } = string.Empty;
    public bool IsDurable { get; init; }
    public string[] Subjects { get; init; } = [];
    public string Stream { get; init; } =  string.Empty;
    public string? QueueGroup { get; init; }
    public Type Consumer { get; init; } = null!;
    public FrozenDictionary<string, (MethodInfo Method, Type MessageType)> Methods { get; init; } = FrozenDictionary<string, (MethodInfo Method, Type MessageType)>.Empty;
    public bool Obsolete { get; init; }
    public DurableConsumerAttribute? Durable { get; init; }
}