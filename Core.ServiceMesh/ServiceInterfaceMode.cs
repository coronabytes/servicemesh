namespace Core.ServiceMesh;

public enum ServiceInterfaceMode
{
    /// <summary>
    ///     Do not register service interface with implementation.
    ///     Use IServiceMesh.CreateProxy&lt;iface&gt;()
    /// </summary>
    None,

    /// <summary>
    ///     Register service interface as local implementation or remote proxy when unavailable.
    /// </summary>
    Auto,

    /// <summary>
    ///     Register service interface with remote or trace proxy implementation.
    ///     Allows open telemetry insights even with local implementations.
    /// </summary>
    AutoTrace,

    /// <summary>
    ///     Always register service interface with remote proxy implementation.
    ///     Allows testing nats networking even with a single instance.
    /// </summary>
    ForceRemote
}