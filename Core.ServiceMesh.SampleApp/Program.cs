using Core.ServiceMesh;
using Core.ServiceMesh.SampleApp.Services;
using Core.ServiceMesh.SampleInterfaces;

var builder = WebApplication.CreateBuilder(args);

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
app.MapControllers();

app.Run();
