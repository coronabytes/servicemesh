using System.Reflection;

namespace Core.ServiceMesh.Proxy;

public abstract class DispatchProxyAsync
{
    public static T Create<T, TProxy>() where TProxy : DispatchProxyAsync
    {
        return (T)AsyncDispatchProxyGenerator.CreateProxyInstance(typeof(TProxy), typeof(T));
    }

    public static object Create(Type interfaceType, Type baseType)
    {
        return AsyncDispatchProxyGenerator.CreateProxyInstance(baseType, interfaceType);
    }

    public abstract object Invoke(MethodInfo method, object[] args);

    public abstract Task InvokeAsync(MethodInfo method, object[] args);

    public abstract Task<T> InvokeAsyncT<T>(MethodInfo method, object[] args);
}