using Core.Observability;
using Core.ServiceMesh;
using SampleWorker.Consumers;

var builder = Host.CreateApplicationBuilder(args);

builder.AddObservability(configureTracing: trace =>
{
    trace.AddSource("core.servicemesh");
});

builder.Services.Configure<ObservabilityOptions>(options =>
{

});

builder.AddServiceMesh(options =>
{
    options.Prefix = "dev";
    options.ConfigureNats = opts => opts with
    {
        Url = "nats://localhost:4222"
    };
    options.ConfigureStream = (name, config) =>
    {
        config.MaxAge = TimeSpan.FromDays(1);
    };
    options.ConfigureConsumer = (name, config, opts) =>
    {
        config.MaxDeliver = 3;
        config.MaxAckPending = 8;

        if (name == "dev-SomeOtherCommandHandler")
        {
            config.MaxDeliver = 3;
            config.MaxAckPending = 1;
        }
    };
    options.InterfaceMode = ServiceInterfaceMode.None;
    options.Assemblies = [typeof(SomeCommandHandler).Assembly];
});

var host = builder.Build();
host.Run();
