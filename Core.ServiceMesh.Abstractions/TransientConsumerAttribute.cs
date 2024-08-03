namespace Core.ServiceMesh.Abstractions;

[AttributeUsage(AttributeTargets.Class)]
public class TransientConsumerAttribute(string? queueGroup = null) : Attribute
{
    public string? QueueGroup => queueGroup;
}