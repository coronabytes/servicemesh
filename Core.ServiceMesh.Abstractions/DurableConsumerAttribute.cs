namespace Core.ServiceMesh.Abstractions;

[AttributeUsage(AttributeTargets.Class)]
public class DurableConsumerAttribute(string name, string stream) : Attribute
{
    public string Name => name;
    public string Stream => stream;
}