[![Nuget](https://img.shields.io/nuget/v/Core.ServiceMesh)](https://www.nuget.org/packages/Core.ServiceMesh)
[![Nuget](https://img.shields.io/nuget/dt/Core.ServiceMesh)](https://www.nuget.org/packages/Core.ServiceMesh)

```
dotnet add package Core.ServiceMesh
```
# Service Mesh for ASP.NET Core
- based on https://nats.io
- service request reponse
  - strongly typed clients out of the box
- event streaming
  - durable and transient consumers

## Initialization in ASP.NET Core

```csharp
builder.AddServiceMesh(options =>
{
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
```

## Service Interface

```csharp
[ServiceMesh("someservice")]
public interface ISomeService
{
    Task<string> GetSomeString(int a, string b);
    Task CreateSomeObject();
    Task<T> GenericAdd<T>(T a, T b) where T : INumber<T>;
};
```

## Service Implementation

```csharp
[ServiceMesh("someservice")]
public class SomeService(ILogger<SomeService> logger) : ISomeService
{
    public async Task<string> GetSomeString(int a, string b)
    {
        await Task.Delay(100);
        return b + " " + a;
    }

    public async Task CreateSomeObject()
    {
        await Task.Delay(100);
        logger.LogInformation(nameof(CreateSomeObject));
    }

    public async Task<T> GenericAdd<T>(T a, T b) where T : INumber<T>
    {
        await Task.Delay(100);
        return a + b;
    }
}
```

## Service Invocation

```csharp
public class DevController(ISomeService someService) : ControllerBase
{
    [HttpPost("add-ints")]
    public async Task<ActionResult<int>> CreateIntObject([FromQuery] int a = 3, [FromQuery] int b = 5)
    {
        return await someService.GenericAdd(a, b);
    }

    [HttpPost("add-doubles")]
    public async Task<ActionResult<double>> CreateDoubleObject([FromQuery] double a = 3.1, [FromQuery] double b = 5.1)
    {
        return await someService.GenericAdd(a, b);
    }
}
```

## Events, Streams and Consumers

```csharp
public record SomeCommand(string Name);

[DurableConsumer("SomeCommandHandler", "default")]
public class SomeCommandHandler(ILogger<SomeCommandHandler> logger) : IConsumer<SomeCommand>
{
    public ValueTask ConsumeAsync(SomeCommand message, CancellationToken token)
    {
        // do stuff
        return ValueTask.CompletedTask;
    }
}

public class DevController(IServiceMesh mesh) : ControllerBase
{
    [HttpPost("publish")]
    public async Task<IActionResult> Publish([FromQuery] string message)
    {
        await mesh.PublishAsync(new SomeCommand(message));
        return Ok();
    }
}
```
