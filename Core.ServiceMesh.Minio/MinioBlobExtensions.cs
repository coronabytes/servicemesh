using Core.ServiceMesh.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Core.ServiceMesh.Minio;

public static class MinioBlobExtensions
{
    public static IServiceCollection AddServiceMeshMinioStorage(this IServiceCollection services,
        Action<MinioBlobOptions> configure)
    {
        var options = new MinioBlobOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton<IBlobProvider, MinioBlobProvider>();

        return services;
    }
}