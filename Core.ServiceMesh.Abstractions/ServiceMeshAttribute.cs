namespace Core.ServiceMesh.Abstractions;

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class)]
public class ServiceMeshAttribute(string name) : Attribute
{
    /// <summary>
    ///  Unique service name.
    ///  May also include versioning information
    ///  e.g. SampleServiceV2
    /// </summary>
    public string Name => name;
}