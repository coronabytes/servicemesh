using System.Numerics;
using Core.ServiceMesh.Abstractions;
using Microsoft.Extensions.Logging;

namespace Core.ServiceMesh.Tests;

[ServiceMesh]
public class TestService : ITestService
{
    public async ValueTask<T> GenericAdd<T>(T a, T b) where T : INumber<T>
    {
        await Task.Delay(100);
        return a + b;
    }

    public async IAsyncEnumerable<int> StreamingResponse(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await Task.Delay(10);
            yield return i;
        }
    }
}