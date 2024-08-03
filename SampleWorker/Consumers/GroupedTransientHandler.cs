using Core.ServiceMesh.Abstractions;
using SampleInterfaces;

namespace SampleWorker.Consumers;

[TransientConsumer("tgroup")]
public class GroupedTransientHandler(ILogger<SomeCommandHandler> logger) : IConsumer<SomeCommand>
{
    public ValueTask ConsumeAsync(SomeCommand message, CancellationToken token)
    {
        logger.LogInformation($"GroupedTransientSomeCommandHandler: {message.Name}");

        return ValueTask.CompletedTask;
    }
}