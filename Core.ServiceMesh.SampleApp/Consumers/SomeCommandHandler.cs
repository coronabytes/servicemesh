using Core.ServiceMesh.Abstractions;
using Core.ServiceMesh.SampleInterfaces;

namespace Core.ServiceMesh.SampleApp.Consumers;

[DurableConsumer("SomeCommandHandler", "default")]
public class SomeCommandHandler(ILogger<SomeCommandHandler> logger) : IConsumer<SomeCommand>
{
    public ValueTask ConsumeAsync(SomeCommand message, CancellationToken token)
    {
        logger.LogInformation($"SomeCommandHandler 1: {message.Name}");

        return ValueTask.CompletedTask;
    }
}