namespace Core.ServiceMesh.Abstractions
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class ServiceMeshAttribute(string name, string? queueGroup = null) : Attribute
    {
        public string Name => name;
        public string? QueueGroup => queueGroup;
    }
}
