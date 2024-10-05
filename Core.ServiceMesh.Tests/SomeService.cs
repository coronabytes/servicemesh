using System.Diagnostics;
using System.Numerics;
using Core.ServiceMesh.Abstractions;
using Microsoft.Extensions.Logging;
using SampleInterfaces;

namespace Core.ServiceMesh.Tests;

[ServiceMesh("someservice")]
public class SomeService(ILogger<SomeService> logger) : ISomeService
{
    public async ValueTask<string> GetSomeString(int a, string b)
    {
        logger.LogInformation("test log");
        Activity.Current?.AddEvent(new ActivityEvent("test 1"));
        await Task.Delay(100);
        Activity.Current?.AddEvent(new ActivityEvent("test 2"));
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

    public ValueTask<SampleResponse> Sample(SampleRequest request)
    {
        return ValueTask.FromResult(new SampleResponse(request.Name + " " + request.Amount));
    }

    public async IAsyncEnumerable<SampleResponse> StreamingResponse(SampleRequest request)
    {
        await Task.Delay(100);
        yield return new SampleResponse("a");
        await Task.Delay(100);
        yield return new SampleResponse("b");
        await Task.Delay(100);
        yield return new SampleResponse("c");
    }
}