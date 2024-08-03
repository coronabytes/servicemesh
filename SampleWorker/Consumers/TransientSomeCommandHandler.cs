using Core.ServiceMesh.Abstractions;
using SampleInterfaces;

namespace SampleWorker.Consumers;

[TransientConsumer]
public class TransientHandler(ILogger<SomeCommandHandler> logger) : IConsumer<SomeCommand>
{
    public ValueTask ConsumeAsync(SomeCommand message, CancellationToken token)
    {
        logger.LogInformation($"TransientSomeCommandHandler: {message.Name}");

        return ValueTask.CompletedTask;
    }
}