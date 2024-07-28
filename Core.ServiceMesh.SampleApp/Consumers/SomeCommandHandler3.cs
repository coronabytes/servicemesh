using Core.ServiceMesh.Abstractions;
using Core.ServiceMesh.SampleInterfaces;

namespace Core.ServiceMesh.SampleApp.Consumers;

[TransientConsumer("default")]
public class SomeCommandHandler3(ILogger<SomeCommandHandler> logger) : IConsumer<SomeCommand>
{
    public ValueTask ConsumeAsync(SomeCommand message, CancellationToken token)
    {
        logger.LogInformation($"SomeCommandHandler 3: {message.Name}");

        return ValueTask.CompletedTask;
    }
}