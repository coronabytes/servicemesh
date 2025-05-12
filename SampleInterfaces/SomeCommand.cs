using Core.ServiceMesh.Abstractions;

namespace SampleInterfaces;

public record SomeCommand(string Name);

public record IndexBlobCommand(BlobRef Blob);