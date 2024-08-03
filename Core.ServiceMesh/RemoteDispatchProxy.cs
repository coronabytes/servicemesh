using System.Reflection;
using Core.ServiceMesh.Internal;
using Core.ServiceMesh.Proxy;

namespace Core.ServiceMesh;

public class RemoteDispatchProxy : DispatchProxyAsync
{
    internal static ServiceMeshWorker Worker { get; set; } = null!;

    public override object Invoke(MethodInfo method, object[] args)
    {
        throw new InvalidOperationException("only async method supported");
    }

    public override Task InvokeAsync(MethodInfo method, object[] args)
    {
        return Worker.RequestAsync(method, args);
    }

    public override Task<T> InvokeAsyncT<T>(MethodInfo method, object[] args)
    {
        return Worker.RequestAsync<T>(method, args);
    }
}