using System.Numerics;
using Core.ServiceMesh.Abstractions;

namespace SampleInterfaces;

[ServiceMesh("someservice")]
public interface ISomeService
{
    Task<string> GetSomeString(int a, string b);
    Task CreateSomeObject();
    Task<T> GenericAdd<T>(T a, T b) where T : INumber<T>;
};