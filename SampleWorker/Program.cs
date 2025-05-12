using Core.Observability;
using Core.ServiceMesh;
using Core.ServiceMesh.Minio;
using Minio;
using SampleWorker.Consumers;

var builder = Host.CreateApplicationBuilder(args);

builder.AddObservability(configureTracing: trace => { trace.AddServiceMeshInstrumentation(); });

builder.Services.Configure<ObservabilityOptions>(options => { });

builder.Services.AddServiceMesh(options =>
{
    options.Prefix = "dev";
    options.ConfigureNats = opts => opts with
    {
        Url = "nats://localhost:4222"
    };
    options.ConfigureStream = (name, config) => { config.MaxAge = TimeSpan.FromDays(1); };
    options.InterfaceMode = ServiceInterfaceMode.None;
    options.Assemblies = [typeof(SomeCommandHandler).Assembly];
});

builder.Services.AddMinio(x => x
    .WithEndpoint("localhost:4223")
    .WithCredentials("minio", "x9ZotJrg5euEp976rG")
    .WithSSL(false)
    .Build());

builder.Services.AddServiceMeshMinioStorage(x =>
{
    x.Bucket = "mesh";
    x.Prefix = "temp/";
});

var host = builder.Build();
host.Run();