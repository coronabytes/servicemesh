using System.Diagnostics;
using SampleInterfaces;

namespace SampleApp.Services;

public class AnotherService(ILogger<AnotherService> logger) : IAnotherService
{
    public Task<SampleResponse> SampleA(SampleRequest request)
    {
        return Task.FromResult(new SampleResponse(request.Name + " " + request.Amount));
    }

    public Task<SampleResponse> SampleB(SampleRequest request)
    {
        return Task.FromResult(new SampleResponse(request.Name + " " + request.Amount));
    }

    public async Task SampleC(SampleRequest request)
    {
        Activity.Current?.AddEvent(new ActivityEvent("#1"));
        await Task.Delay(100);
        Activity.Current?.AddEvent(new ActivityEvent("#2"));
    }
}