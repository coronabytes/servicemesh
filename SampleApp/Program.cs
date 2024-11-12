using Core.Observability;
using Core.ServiceMesh;
using SampleApp.Services;
using SampleInterfaces;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

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
    options.InterfaceMode = ServiceInterfaceMode.ForceRemote;
    options.Assemblies = [typeof(ISomeService).Assembly, typeof(SomeService).Assembly];
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

app.UseAuthorization();
app.UseObservability();
app.MapControllers();

//app.MapServiceMesh(["Command", "Message"]);

app.Run();