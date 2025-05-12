namespace Core.ServiceMesh.Abstractions;

/// <summary>
///   Standardized way to send large blobs
/// </summary>
public record BlobRef(string PreSignedUrl, string ContentType, long Size);