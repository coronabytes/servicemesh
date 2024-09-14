using System.Diagnostics;

namespace Core.ServiceMesh.Abstractions;

public static class ServiceMeshActivity
{
    public static readonly ActivitySource Source = new("core.servicemesh");
}