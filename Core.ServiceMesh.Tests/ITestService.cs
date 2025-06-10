using System.Numerics;
using Core.ServiceMesh.Abstractions;

namespace Core.ServiceMesh.Tests;

[ServiceMesh("testservice")]
public interface ITestService
{
    ValueTask<T> GenericAdd<T>(T a, T b) where T : INumber<T>;
    IAsyncEnumerable<int> StreamingResponse(int count);

    ValueTask<List<Guid>> GetIds(Guid? id = null);
}