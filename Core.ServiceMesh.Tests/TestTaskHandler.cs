using Core.ServiceMesh.Abstractions;

namespace Core.ServiceMesh.Tests;

[DurableConsumer("TestTaskHandler", Stream = "testtasks")]
// ReSharper disable once UnusedMember.Global
public class TestTaskHandler : IConsumer<TestTask>
{
    //public static AutoResetEvent Event = new(false);
    public static TaskCompletionSource<bool> Source = new(false);

    public ValueTask ConsumeAsync(TestTask message, CancellationToken token)
    {
        Source.SetResult(true);

        return ValueTask.CompletedTask;
    }
}