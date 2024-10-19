using Core.ServiceMesh.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit.Abstractions;

namespace Core.ServiceMesh.Tests;

[CollectionDefinition("UnitTest1", DisableParallelization = true)]
public class UnitTest1(ITestOutputHelper logger) : IAsyncLifetime
{
    private readonly CancellationTokenSource _cancellation = new();
    private IServiceMesh? _mesh;
    private IServiceProvider? _serviceProvider;
    private BackgroundService? _worker;

    public async Task InitializeAsync()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddServiceMesh(options =>
        {
            options.Prefix = Guid.NewGuid().ToString("N");
            options.ConfigureNats = opts => opts with
            {
                Url = "nats://localhost:4222"
            };
            options.ConfigureStream = (name, config) => { config.MaxAge = TimeSpan.FromMinutes(10); };
            options.InterfaceMode = ServiceInterfaceMode.None;
            options.Assemblies = [typeof(ITestService).Assembly];
        });

        _serviceProvider = serviceCollection.BuildServiceProvider(true);
        _mesh = _serviceProvider.GetRequiredService<IServiceMesh>();

        _worker = (BackgroundService)_mesh;
        logger.WriteLine("starting background worker");
        await _worker.StartAsync(_cancellation.Token);
        await Task.Delay(1000);
    }

    public async Task DisposeAsync()
    {
        logger.WriteLine("aborting background worker");
        await _cancellation.CancelAsync();

        try
        {
            await _worker!.ExecuteTask!;
        }
        catch
        {
            // NOP
        }
    }

    [Fact]
    public async Task GenericAddInt()
    {
        var someService = _mesh!.CreateProxy<ITestService>();

        var res = await someService.GenericAdd(2, 4);

        Assert.Equal(6, res);
    }

    [Fact]
    public async Task GenericAddDecimal()
    {
        var someService = _mesh!.CreateProxy<ITestService>();

        var res = await someService.GenericAdd(6m, 6m);

        Assert.Equal(12m, res);
    }

    [Fact]
    public async Task StreamResponse()
    {
        var someService = _mesh!.CreateProxy<ITestService>();

        var list1 = new List<int>();

        await foreach (var r in someService.StreamingResponse(3))
            list1.Add(r);

        Assert.Equal(3, list1.Count);

        var list2 = new List<int>();

        await foreach (var r in someService.StreamingResponse(5))
            list2.Add(r);

        Assert.Equal(5, list2.Count);
    }

    [Fact]
    public async Task TaskTest()
    {
        await _mesh!.SendAsync(new TestTask(3));

        var res = await TestTaskHandler.Source.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(res);
    }
}