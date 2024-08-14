namespace Core.ServiceMesh.Abstractions
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class ServiceMeshAttribute(string name) : Attribute
    {
        public string Name => name;
        public string? QueueGroup { get; set; }
    }
}
