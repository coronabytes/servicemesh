using System.Numerics;
using Core.ServiceMesh.Abstractions;
using SampleInterfaces;

namespace Core.ServiceMesh.SampleApp.Services;

[ServiceMesh("someservice", "someservice")]
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