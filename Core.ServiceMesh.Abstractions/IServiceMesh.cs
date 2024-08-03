namespace Core.ServiceMesh.Abstractions;

public interface IServiceMesh
{
    /// <summary>
    ///    Create remote proxy for service interface.
    /// </summary>
    /// <typeparam name="T">service interface</typeparam>
    /// <returns>proxy service implementation</returns>
    T CreateProxy<T>() where T : class;

    /// <summary>
    ///   publish message and await confirmation.
    ///   requires at least one consumer or timeout.
    /// </summary>
    ValueTask PublishAsync(object message, int retry = 3, TimeSpan? retryWait = null, string? id = null);

    /// <summary>
    ///   send message and ignore confirmation
    /// </summary>
    ValueTask SendAsync(object message);
}