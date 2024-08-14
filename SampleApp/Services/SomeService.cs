using System.Diagnostics;
using System.Numerics;
using SampleInterfaces;

namespace SampleApp.Services;

public class SomeService(ILogger<SomeService> logger) : ISomeService
{
    public async Task<string> GetSomeString(int a, string b)
    {
        logger.LogInformation("test log");
        Activity.Current?.AddEvent(new ActivityEvent("test 1"));
        await Task.Delay(100);
        Activity.Current?.AddEvent(new ActivityEvent("test 2"));
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

    public Task<SampleResponse> Sample(SampleRequest request)
    {
        return Task.FromResult(new SampleResponse(request.Name + " " + request.Amount));
    }
}