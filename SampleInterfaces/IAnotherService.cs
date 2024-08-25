using Core.ServiceMesh.Abstractions;

namespace SampleInterfaces;

[ServiceMesh("anotherservice")]
public interface IAnotherService
{
    ValueTask<SampleResponse> SampleA(SampleRequest request);

    ValueTask<SampleResponse> SampleB(SampleRequest request);

    ValueTask SampleC(SampleRequest request);
};