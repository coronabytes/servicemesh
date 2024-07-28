namespace Core.ServiceMesh;

public enum ServiceRegisterMode
{
    /// <summary>
    ///   do not register service interface with implementation
    /// </summary>
    None,
    /// <summary>
    ///   register service interface as local implementation or proxy when unavailable
    /// </summary>
    Auto,
    /// <summary>
    ///   register service interface with proxy implementation
    /// </summary>
    ForceProxy
}