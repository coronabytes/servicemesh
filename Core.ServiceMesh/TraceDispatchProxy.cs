using System.Diagnostics;
using System.Reflection;
using Core.ServiceMesh.Abstractions;
using Core.ServiceMesh.Internal;
using Core.ServiceMesh.Proxy;
using Microsoft.Extensions.DependencyInjection;

namespace Core.ServiceMesh;

public class TraceDispatchProxy : DispatchProxyAsync
{
    internal IServiceProvider ServiceProvider { get; set; } = null!;
    internal Type ImplementationType { get; set; } = null!;

    public override object Invoke(MethodInfo method, object[] args)
    {
        throw new InvalidOperationException("only async method supported");
    }

    public override Task InvokeAsync(MethodInfo method, object[] args)
    {
        using var scope = ServiceProvider.CreateScope();
        var instance = scope.ServiceProvider.GetRequiredService(ImplementationType);

        var attr = method.DeclaringType!.GetInterfaces().Single().GetCustomAttribute<ServiceMeshAttribute>()!;
        using var activity = ServiceMeshWorker.ActivitySource.StartActivity($"{attr.Name}.{method.Name}", ActivityKind.Internal, Activity.Current?.Context ?? default);

        return (Task)method.Invoke(instance, args)!;
    }

    public override Task<T> InvokeAsyncT<T>(MethodInfo method, object[] args)
    {
        using var scope = ServiceProvider.CreateScope();
        var instance = scope.ServiceProvider.GetRequiredService(ImplementationType);

        var attr = method.DeclaringType!.GetInterfaces().Single().GetCustomAttribute<ServiceMeshAttribute>()!;

        using var activity = ServiceMeshWorker.ActivitySource.StartActivity($"{attr.Name}.{method.Name}", ActivityKind.Internal, Activity.Current?.Context ?? default);

        //if (activity != null)
        //{
        //    activity.DisplayName = $"{attr.Name}.{method.Name}";
        //}

        return (Task<T>)method.Invoke(instance, args)!;
    }
}