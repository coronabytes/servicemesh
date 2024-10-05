using Core.ServiceMesh.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SampleInterfaces;
using Xunit.Abstractions;

namespace Core.ServiceMesh.Tests;

[CollectionDefinition("UnitTest1", DisableParallelization = true)]
public class UnitTest1(ITestOutputHelper logger) : IAsyncLifetime
{
    private readonly CancellationTokenSource _cancellation = new();
    private IServiceMesh _mesh;
    private IServiceProvider _serviceProvider;
    private BackgroundService _worker;

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
            options.Assemblies = [typeof(ISomeService).Assembly, typeof(SomeService).Assembly];
        });

        _serviceProvider = serviceCollection.BuildServiceProvider(true);
        _mesh = _serviceProvider.GetRequiredService<IServiceMesh>();

        _worker = (BackgroundService)_mesh;
        await _worker.StartAsync(_cancellation.Token);
    }

    public async Task DisposeAsync()
    {
        await _cancellation.CancelAsync();

        try
        {
            await _worker.ExecuteTask!;
        }
        catch
        {
            // NOP
        }
    }

    [Fact]
    public async Task Test1()
    {
        var someService = _mesh.CreateProxy<ISomeService>();

        var res = await someService.GenericAdd(2, 4);

        Assert.Equal(6, res);
    }

    [Fact]
    public async Task Test2()
    {
        var someService = _mesh.CreateProxy<ISomeService>();

        var res = await someService.GenericAdd(6m, 6m);

        Assert.Equal(12m, res);
    }

    [Fact]
    public async Task Test3()
    {
        var someService = _mesh.CreateProxy<ISomeService>();

        var list = new List<SampleResponse>();

        await foreach (var r in someService.StreamingResponse(new SampleRequest("Bla", 133.7m)))
            list.Add(r);

        Assert.Equal(3, list.Count);
    }
}