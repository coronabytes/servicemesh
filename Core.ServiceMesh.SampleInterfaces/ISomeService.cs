using Core.ServiceMesh.Abstractions;
using System.Numerics;

namespace Core.ServiceMesh.SampleInterfaces;

[ServiceMesh("someservice")]
public interface ISomeService
{
    Task<string> GetSomeString(int a, string b);
    Task CreateSomeObject();

    Task<T> GenericAdd<T>(T a, T b) where T : INumber<T>;
};