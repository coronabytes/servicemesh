using System.Numerics;
using Core.ServiceMesh.Abstractions;
using Microsoft.Extensions.Logging;

namespace Core.ServiceMesh.CLI;

[ServiceMesh]
public class TestService(ILogger<TestService> logger) : ITestService
{
    public async ValueTask<T> GenericAdd<T>(T a, T b) where T : INumber<T>
    {
        logger.LogInformation("Hello");
        await Task.Delay(100);
        return a + b;
    }
}