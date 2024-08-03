using Core.ServiceMesh.Abstractions;
using SampleInterfaces;

namespace SampleWorker.Consumers;

[DurableConsumer("MultiCommandHandler", Stream = "default")]
public class MultiCommandHandler(ILogger<SomeOtherCommandHandler> logger) 
    : IConsumer<SomeOtherCommand>, IConsumer<SomeCommand>
{
    public async ValueTask ConsumeAsync(SomeOtherCommand message, CancellationToken token)
    {
        logger.LogInformation($"MultiCommandHandler: {message.Name}");
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    public async ValueTask ConsumeAsync(SomeCommand message, CancellationToken token)
    {
        logger.LogInformation($"MultiCommandHandler: {message.Name}");
        await Task.Delay(TimeSpan.FromSeconds(1));
    }
}