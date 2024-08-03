using Core.ServiceMesh.Abstractions;
using SampleInterfaces;

namespace SampleWorker.Consumers;

[DurableConsumer("SomeOtherCommandHandler", 
    Stream = "default", 
    DeliverPolicy = DeliverPolicy.All, 
    MaxAckPending = 1,
    MaxAckWait = 60*5)]
public class SomeOtherCommandHandler(ILogger<SomeOtherCommandHandler> logger) : IConsumer<SomeOtherCommand>
{
    public async ValueTask ConsumeAsync(SomeOtherCommand message, CancellationToken token)
    {
        logger.LogInformation($"SomeOtherCommandHandler: {message.Name}");
        await Task.Delay(TimeSpan.FromSeconds(5));
    }
}