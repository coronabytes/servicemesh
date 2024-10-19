using System.Diagnostics;
using Core.ServiceMesh.Abstractions;
using SampleInterfaces;

namespace SampleApp.Services;

[ServiceMesh]
public class AnotherService(ILogger<AnotherService> logger) : IAnotherService
{
    public ValueTask<SampleResponse> SampleA(SampleRequest request)
    {
        return ValueTask.FromResult(new SampleResponse(request.Name + " " + request.Amount));
    }

    public ValueTask<SampleResponse> SampleB(SampleRequest request)
    {
        return ValueTask.FromResult(new SampleResponse(request.Name + " " + request.Amount));
    }

    public async ValueTask SampleC(SampleRequest request)
    {
        Activity.Current?.AddEvent(new ActivityEvent("#1"));
        await Task.Delay(100);
        Activity.Current?.AddEvent(new ActivityEvent("#2"));
    }
}