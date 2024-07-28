using System.Reflection;

namespace Core.ServiceMesh.Internal;

internal class ServiceRegistration
{
    public string Subject { get; init; } = string.Empty;
    public Type InterfaceType { get; init; } = null!;
    public Type ImplementationType { get; init; } = null!;
    public MethodInfo Method { get; init; } = null!;
    public string QueueGroup { get; init; } = string.Empty;
}