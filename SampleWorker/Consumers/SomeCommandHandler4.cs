using Core.ServiceMesh.Abstractions;
using SampleInterfaces;

namespace SampleWorker.Consumers;

[DurableConsumer("SomeOtherCommandHandler", "default")]
public class SomeOtherCommandHandler(ILogger<SomeOtherCommandHandler> logger) : IConsumer<SomeOtherCommand>
{
    public async ValueTask ConsumeAsync(SomeOtherCommand message, CancellationToken token)
    {
        logger.LogInformation($"SomeOtherCommandHandler: {message.Name}");
        await Task.Delay(TimeSpan.FromSeconds(5));
    }
}