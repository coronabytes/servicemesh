namespace Core.ServiceMesh.Abstractions;

public interface IServiceMesh
{
    /// <summary>
    ///    create remote proxy for service interface
    /// </summary>
    T CreateProxy<T>() where T : class;

    /// <summary>
    ///   publish message and await confirmation.
    ///   requires at least one consumer or timeout.
    /// </summary>
    ValueTask PublishAsync(object message, int retry = 3, TimeSpan? retryWait = null);

    /// <summary>
    ///   send message and ignore confirmation
    /// </summary>
    ValueTask SendAsync(object message);
}