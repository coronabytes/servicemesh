using Core.Observability;
using Core.ServiceMesh;
using Core.ServiceMesh.SampleApp.Services;
using SampleInterfaces;

var builder = WebApplication.CreateBuilder(args);

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
    options.Nats = "nats://localhost:4222";
    options.RegisterServiceInterface = ServiceRegisterMode.ForceProxy;
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

app.Run();