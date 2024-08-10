using Core.Observability;
using Core.ServiceMesh;
using Core.ServiceMesh.Abstractions;
using SampleApp.Services;
using SampleInterfaces;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability(configureTracing: trace =>
{
    trace.AddServiceMeshInstrumentation();
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
    options.InterfaceMode = ServiceInterfaceMode.Auto;
    options.Assemblies = [typeof(ISomeService).Assembly, typeof(SomeService).Assembly];
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();
app.UseObservability();
app.MapControllers();

app.MapServiceMesh();

app.Run();