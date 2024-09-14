using Core.ServiceMesh.Abstractions;
using SampleInterfaces;

namespace SampleAppNuget.Mesh;

[ServiceMesh("nugetservice")]
public interface INugetService
{
    ValueTask<SampleResponse> SampleA(SampleRequest request);

    ValueTask<SampleResponse> SampleB(SampleRequest request);

    ValueTask SampleC(SampleRequest request);
}