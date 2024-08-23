﻿using System.Reflection;

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
    ///   Publish message and await confirmation.
    ///   Requires at least one consumer or timeout.
    /// </summary>
    ValueTask PublishAsync(object message, int retry = 3, TimeSpan? retryWait = null, string? id = null);

    /// <summary>
    ///   Send message and ignore confirmation
    /// </summary>
    ValueTask SendAsync(object message);

    [Obsolete]
    Task<T> RequestAsync<T>(MethodInfo info, object[] args);
    [Obsolete]
    Task RequestAsync(MethodInfo info, object[] args);

    ValueTask<T> RequestAsync<T>(string subject, object[] args);
    ValueTask RequestAsync(string subject, object[] args);
    IAsyncEnumerable<T> StreamAsync<T>(string subject, object[] args);
}