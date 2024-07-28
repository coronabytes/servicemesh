using System.Reflection;

namespace Core.ServiceMesh.Internal;

internal class ConsumerRegistration
{
    public string Name { get; init; }
    public bool IsDurable { get; init; }
    public string Subject { get; init; }
    public string Stream { get; init; }
    public Type Type { get; init; }
    public Type Consumer { get; init; }
    public MethodInfo Method { get; init; }
    public bool Obsolete { get; init; }
}