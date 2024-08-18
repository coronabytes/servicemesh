using Core.ServiceMesh.Abstractions;

namespace SampleInterfaces;

[ServiceMesh("anotherservice")]
public interface IAnotherService
{
    Task<SampleResponse> SampleA(SampleRequest request);

    Task<SampleResponse> SampleB(SampleRequest request);

    Task SampleC(SampleRequest request);
};