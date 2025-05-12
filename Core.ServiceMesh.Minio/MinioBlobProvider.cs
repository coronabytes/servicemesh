using Core.ServiceMesh.Abstractions;
using Minio;
using Minio.DataModel.Args;

namespace Core.ServiceMesh.Minio;

internal class MinioBlobProvider(IMinioClient minio, MinioBlobOptions options) : IBlobProvider
{
    public async ValueTask<BlobRef> UploadBlobAsync(Stream readStream, string contentType, TimeSpan? expire, CancellationToken cancellationToken = default)
    {
        var id = Guid.CreateVersion7();

        var path = $"{options.Prefix ?? string.Empty}{id:N}.blob";

        var res1 = await minio.PutObjectAsync(new PutObjectArgs()
            .WithBucket(options.Bucket)
            .WithObject(path)
            .WithStreamData(readStream)
            .WithObjectSize(-1)
            .WithContentType(contentType), cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var res2 = await minio.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(options.Bucket)
            .WithObject(path)
            .WithExpiry(((int?)expire?.TotalSeconds) ?? (3600 * 24)));

        return new BlobRef(res2, contentType, res1.Size);
    }

    public async ValueTask DownloadBlobAsync(BlobRef blob, Stream writeStream, CancellationToken cancellationToken = default)
    {
        var res = await minio.WrapperGetAsync(blob.PreSignedUrl);
        var stream = await res.Content.ReadAsStreamAsync(cancellationToken);
        await stream.CopyToAsync(writeStream, cancellationToken);
    }
}