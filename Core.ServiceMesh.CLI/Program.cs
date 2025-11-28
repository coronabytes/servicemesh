using Core.ServiceMesh;
using Core.ServiceMesh.Abstractions;
using Core.ServiceMesh.CLI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var isClient = args[0] == "client";
var isServer = args[0] == "server";
var useGateway = args.Contains("gateway");

CancellationTokenSource cancellation = new();

Console.CancelKeyPress += delegate {
    cancellation.Cancel();
};

var serviceCollection = new ServiceCollection();

serviceCollection.AddServiceMesh(options =>
{
    options.Prefix = "cli";
    options.ConfigureNats = opts => opts with
    {
        Url = useGateway ? "nats://localhost:4224" : "nats://localhost:4222"
    };
    options.ConfigureStream = (name, config) => { config.MaxAge = TimeSpan.FromMinutes(10); };
    options.InterfaceMode = ServiceInterfaceMode.ForceRemote;
    options.Assemblies = [typeof(Program).Assembly];
    options.DeveloperMode = isClient;
});
serviceCollection.AddLogging(options => options.AddConsole());

var serviceProvider = serviceCollection.BuildServiceProvider(true);
var mesh = serviceProvider.GetRequiredService<IServiceMesh>();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

var worker = (BackgroundService)mesh;

await worker.StartAsync(cancellation.Token);

if (isServer)
{
    logger.LogInformation("server");
}

else if (isClient)
{
    logger.LogInformation("client");

    for (int i = 0; i < 100; i++)
    {
        await Task.Delay(1000);

        var someService = mesh.CreateProxy<ITestService>();
        await someService.GenericAdd(1, 1);
    }
}

try
{
    await worker!.ExecuteTask!;
}
catch
{
    // NOP
}