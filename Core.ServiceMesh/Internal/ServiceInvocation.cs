namespace Core.ServiceMesh.Internal;

internal class ServiceInvocation
{
    //public string Service { get; init; } = string.Empty;

    //public string Method { get; init; } = string.Empty;

    public List<string?> Signature { get; init; } = [];

    public List<byte[]?> Arguments { get; init; } = [];

    public List<string> Generics { get; init; } = [];
}