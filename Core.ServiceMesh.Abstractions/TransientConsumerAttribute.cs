namespace Core.ServiceMesh.Abstractions;

[AttributeUsage(AttributeTargets.Class)]
public class TransientConsumerAttribute(string queueGroup) : Attribute
{
    public string QueueGroup => queueGroup;
}