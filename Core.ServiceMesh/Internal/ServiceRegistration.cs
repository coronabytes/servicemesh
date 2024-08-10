using System.Collections.Frozen;
using System.Reflection;

namespace Core.ServiceMesh.Internal;

internal class ServiceRegistration
{
    public string Name { get; init; }
    public string Sub { get; init; }

    //public string Subject { get; init; } = string.Empty;
    public Type InterfaceType { get; init; } = null!;
    public Type ImplementationType { get; init; } = null!;
    public FrozenDictionary<string, MethodInfo> Methods { get; init; }
    public string QueueGroup { get; init; } = string.Empty;
}