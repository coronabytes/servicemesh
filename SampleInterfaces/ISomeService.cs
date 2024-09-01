using System.Numerics;
using Core.ServiceMesh.Abstractions;

namespace SampleInterfaces;

[ServiceMesh("someservice")]
public interface ISomeService
{
    ValueTask<string> GetSomeString(int a, string b);
    ValueTask CreateSomeObject();
    ValueTask<T> GenericAdd<T>(T a, T b) where T : INumber<T>;
    ValueTask<SampleResponse> Sample(SampleRequest request);
};