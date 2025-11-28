using System.Numerics;
using Core.ServiceMesh.Abstractions;

namespace Core.ServiceMesh.CLI;

[ServiceMesh("testservice")]
public interface ITestService
{
    ValueTask<T> GenericAdd<T>(T a, T b) where T : INumber<T>;
}