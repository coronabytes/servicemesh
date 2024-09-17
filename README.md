[![Nuget](https://img.shields.io/nuget/v/Core.ServiceMesh)](https://www.nuget.org/packages/Core.ServiceMesh)
[![Nuget](https://img.shields.io/nuget/dt/Core.ServiceMesh)](https://www.nuget.org/packages/Core.ServiceMesh)

# Installation for containers
```
dotnet add package Core.ServiceMesh
dotnet add package Core.ServiceMesh.SourceGen
```

# Install for shared libraries
```
dotnet add package Core.ServiceMesh.Abstractions
dotnet add package Core.ServiceMesh.SourceGen
```

# Service Mesh for ASP.NET Core
- interconnect microservices sync/async with ease
  - based on https://nats.io
- uses source generators for remote proxies
  - this doesn't mean AOT is supported
- service request reponse pattern (sync)
  - strongly typed clients out of the box
  - response streaming with IAsyncEnumerable
- event streaming via NATS JetStream (async)
  - durable and transient consumers
- open telemetry support
  - supports local service traces in "AutoTrace" mode

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
- service interfaces/contracts should be placed in abstraction libraries to be shared among your microservices
- only ValueTask and ValueTask\<T\> and IAsyncEnumerable<T> supported
- open generics with optional constraints are supported
```csharp
[ServiceMesh("someservice")]
public interface ISomeService
{
    ValueTask<string> GetSomeString(int a, string b);
    ValueTask CreateSomeObject();
    ValueTask<T> GenericAdd<T>(T a, T b) where T : INumber<T>;
    IAsyncEnumerable<string> StreamResponse();
};
```

## Service Implementation

```csharp
[ServiceMesh("someservice")]
public class SomeService(ILogger<SomeService> logger) : ISomeService
{
    public async ValueTask<string> GetSomeString(int a, string b)
    {
        await Task.Delay(100);
        return b + " " + a;
    }

    public async ValueTask CreateSomeObject()
    {
        await Task.Delay(100);
        logger.LogInformation(nameof(CreateSomeObject));
    }

    public async ValueTask<T> GenericAdd<T>(T a, T b) where T : INumber<T>
    {
        await Task.Delay(100);
        return a + b;
    }

    public async IAsyncEnumerable<string> StreamResponse()
    {
        await Task.Delay(100);
        yield return "a";
        await Task.Delay(100);
        yield return "b";
        await Task.Delay(100);
        yield return "c";
    }
}
```

## Service Invocation
- inject service interface into your controllers/services
- they are automatically proxied over nats when not available in the same container 
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
- durable consumers need to have a unique name (so you can rename your class later on)
- PublishAsync will await confirmation by nats broker
- SendAsync means Fire and Forget

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
        await mesh.SendAsync(new SomeCommand(message));
        return Ok();
    }
}
```

## (Experimental) HTTP Endpoints
- to lazy to write controllers?
- services and consumer messages may be exposed directly via http endpoints
  - for services only methods with a single complex parameter are supported
  - no generics
  - no simple types

## expose services and messages
- for this example types ending with ..Command or ..Message will be exposed as endpoints
```csharp
app.MapServiceMesh(["Command", "Message"]);
```

## customize or filter http endpoints
```csharp
builder.AddServiceMesh(options =>
{
  options.MapHttpPublishRoute =
        (app, type, handler) =>
        {
            app.MapPost("/api/publish/" + type.Name, handler)
                .WithTags("publish");
        };

  options.MapHttpSendRoute =
        (app, type, handler) =>
        {
            // no handlers without nats ack
            //app.MapPost("/api/send/" + type.Name, handler).WithTags("send");
        };

  options.MapHttpRequestRoute = { get; set; } =
        (app, requestType, responseType, service, method, handler) =>
        {
            app.MapPost("/api/" + service + "/" + method.Name, handler)
                .Produces(200, responseType)
                .WithTags(service);
        };
});
```

