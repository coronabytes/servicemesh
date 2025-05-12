using System.Text;
using Core.ServiceMesh.Abstractions;
using Microsoft.VisualBasic;
using SampleInterfaces;

namespace SampleWorker.Consumers;

[DurableConsumer("SomeCommandHandler", Stream = "default")]
public class SomeCommandHandler(ILogger<SomeCommandHandler> logger) : IConsumer<SomeCommand>
{
    public ValueTask ConsumeAsync(SomeCommand message, CancellationToken token)
    {
        logger.LogInformation($"SomeCommandHandler 1: {message.Name}");

        return ValueTask.CompletedTask;
    }
}

[DurableConsumer("IndexBlobCommandHandler", Stream = "default")]
public class IndexBlobCommandHandler(IServiceMesh mesh, ILogger<IndexBlobCommandHandler> logger) : IConsumer<IndexBlobCommand>
{
    public async ValueTask ConsumeAsync(IndexBlobCommand message, CancellationToken token)
    {
        await using var ms = new MemoryStream();
        await mesh.DownloadBlobAsync(message.Blob, ms, token);

        var s = Encoding.UTF8.GetString(ms.ToArray());

        logger.LogInformation($"Blob: {s}");
    }
}