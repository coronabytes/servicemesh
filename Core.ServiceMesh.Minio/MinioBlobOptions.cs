namespace Core.ServiceMesh.Minio;

public class MinioBlobOptions
{
    public string Bucket { get; set; } = string.Empty;
    public string? Prefix { get; set; }
}