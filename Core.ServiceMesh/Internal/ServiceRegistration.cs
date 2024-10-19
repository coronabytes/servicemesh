using System.Collections.Frozen;
using System.Reflection;

namespace Core.ServiceMesh.Internal;

internal class ServiceRegistration
{
    public string Name { get; init; } = string.Empty;
    public string Sub { get; init; } = string.Empty;

    //public string Subject { get; init; } = string.Empty;
    public Type InterfaceType { get; init; } = null!;
    public Type ImplementationType { get; init; } = null!;
    public FrozenDictionary<string, MethodInfo> Methods { get; init; } = FrozenDictionary<string, MethodInfo>.Empty;
    public string QueueGroup { get; init; } = string.Empty;
}