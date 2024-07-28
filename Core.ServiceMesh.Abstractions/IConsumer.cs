namespace Core.ServiceMesh.Abstractions;

public interface IConsumer<in T>
{
    public ValueTask ConsumeAsync(T message, CancellationToken token);
}