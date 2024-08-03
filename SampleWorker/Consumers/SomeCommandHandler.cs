using Core.ServiceMesh.Abstractions;
using SampleInterfaces;

namespace SampleWorker.Consumers;

[DurableConsumer("SomeCommandHandler", "default")]
public class SomeCommandHandler(ILogger<SomeCommandHandler> logger) : IConsumer<SomeCommand>
{
    public ValueTask ConsumeAsync(SomeCommand message, CancellationToken token)
    {
        logger.LogInformation($"SomeCommandHandler 1: {message.Name}");

        return ValueTask.CompletedTask;
    }
}