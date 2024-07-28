using Core.ServiceMesh.Abstractions;
using Core.ServiceMesh.SampleApp.Services;
using Core.ServiceMesh.SampleInterfaces;

namespace Core.ServiceMesh.SampleApp.Consumers;

[DurableConsumer("SomeCommandHandler2", "default")]
[Obsolete] // deregister this consumer
public class SomeCommandHandler2(ILogger<SomeCommandHandler2> logger) : IConsumer<SomeCommand>
{
    public ValueTask ConsumeAsync(SomeCommand message, CancellationToken token)
    {
        logger.LogInformation($"SomeCommandHandler 2: {message.Name}");

        return ValueTask.CompletedTask;
    }
}