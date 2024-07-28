using System.Reflection;
using System.Threading.Channels;
using Core.ServiceMesh.Abstractions;
using Core.ServiceMesh.Proxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace Core.ServiceMesh.Internal;

internal class ServiceMeshWorker(
    NatsConnection nats,
    IServiceProvider serviceProvider,
    ILogger<ServiceMeshWorker> logger,
    ServiceMeshOptions options) : BackgroundService, IServiceMesh
{
    private readonly NatsJSContext _jetStream = new(nats);
    private Channel<(NatsMsg<byte[]>, ConsumerRegistration)>? _broadcastChannel;
    private Channel<(NatsMsg<byte[]>, ServiceRegistration)>? _serviceChannel;
    private Channel<(NatsJSMsg<byte[]>, ConsumerRegistration)>? _streamChannel;

    public T CreateProxy<T>() where T : class
    {
        return DispatchProxyAsync.Create<T, MeshDispatchProxy>();
    }

    public async ValueTask PublishAsync(object message, int retry = 3, TimeSpan? retryWait = null)
    {
        var subject = ApplyPrefix(options.ResolveSubject(message.GetType()));
        var data = options.Serialize(message, true);

        var res = await _jetStream.PublishAsync(subject, data, opts: new NatsJSPubOpts
        {
            MsgId = Guid.NewGuid().ToString("N"),
            RetryAttempts = retry,
            RetryWaitBetweenAttempts = retryWait ?? TimeSpan.FromSeconds(30)
        });

        res.EnsureSuccess();
    }

    public async ValueTask SendAsync(object message)
    {
        var subject = ApplyPrefix(options.ResolveSubject(message.GetType()));
        var data = options.Serialize(message, true);

        await nats.PublishAsync(subject, data);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        MeshDispatchProxy.Worker = this;

        _streamChannel = Channel.CreateBounded<(NatsJSMsg<byte[]>, ConsumerRegistration)>(10);
        _broadcastChannel = Channel.CreateBounded<(NatsMsg<byte[]>, ConsumerRegistration)>(10);
        _serviceChannel = Channel.CreateBounded<(NatsMsg<byte[]>, ServiceRegistration)>(10);

        var tasks = new List<Task>();

        foreach (var service in ServiceMeshExtensions.Services)
            tasks.Add(ServiceListener(service, stoppingToken));

        var streams = ServiceMeshExtensions.Consumers
            .Where(x => x is { IsDurable: true, Obsolete: false })
            .GroupBy(x => x.Stream);

        foreach (var stream in streams)
        {
            var subjects = stream.Select(x => x.Subject).Distinct().ToList();

            await _jetStream.CreateStreamAsync(new StreamConfig(stream.Key, subjects)
            {
                Storage = StreamConfigStorage.File,
                DuplicateWindow = TimeSpan.FromMinutes(10),
                MaxAge = TimeSpan.FromDays(14)
            }, stoppingToken);
        }

        var consumeOpts = new NatsJSConsumeOpts
        {
            MaxMsgs = 10,
            MaxBytes = null,
            Expires = TimeSpan.FromSeconds(10),
            IdleHeartbeat = TimeSpan.FromSeconds(5)
        };

        foreach (var durableConsumer in ServiceMeshExtensions.Consumers.Where(x => x.IsDurable))
            if (durableConsumer.Obsolete)
            {
                try
                {
                    var existing = 0;
                    var deleted = 0;

                    await foreach (var consumer in _jetStream.ListConsumersAsync(durableConsumer.Stream, stoppingToken))
                        if (consumer.Info.Name == durableConsumer.Name)
                        {
                            await _jetStream.DeleteConsumerAsync(durableConsumer.Stream, durableConsumer.Name,
                                stoppingToken);
                            ++deleted;
                        }
                        else
                        {
                            ++existing;
                        }

                    if (deleted > 1 && existing == 0)
                        await _jetStream.DeleteStreamAsync(durableConsumer.Stream, stoppingToken);
                }
                catch (Exception)
                {
                    // NOP
                }
            }
            else
            {
                var con = await _jetStream.CreateOrUpdateConsumerAsync(durableConsumer.Stream, new ConsumerConfig
                {
                    Name = durableConsumer.Name,
                    DurableName = durableConsumer.Name,
                    AckPolicy = ConsumerConfigAckPolicy.Explicit,
                    MaxDeliver = 3,
                    MaxAckPending = 8
                }, stoppingToken).ConfigureAwait(false);

                tasks.Add(StreamListener(con, consumeOpts, durableConsumer, stoppingToken));
            }

        foreach (var transientConsumer in ServiceMeshExtensions.Consumers.Where(x => !x.IsDurable))
            tasks.Add(BroadcastListener(transientConsumer, stoppingToken));

        for (var i = 0; i < options.StreamWorkers; i++)
            tasks.Add(StreamWorker(stoppingToken));
        for (var i = 0; i < options.ServiceWorkers; i++)
            tasks.Add(ServiceWorker(stoppingToken));
        for (var i = 0; i < options.BroadcastWorkers; i++)
            tasks.Add(BroadcastWorker(stoppingToken));

        await Task.WhenAll(tasks);

        _streamChannel.Writer.Complete();
        _serviceChannel.Writer.Complete();
        _broadcastChannel.Writer.Complete();
    }

    private async Task ServiceListener(ServiceRegistration reg,
        CancellationToken stoppingToken)
    {
        await foreach (var msg in nats!.SubscribeAsync<byte[]>(reg.Subject, reg.QueueGroup,
                           cancellationToken: stoppingToken))
            await _serviceChannel!.Writer.WriteAsync((msg, reg), stoppingToken);
    }

    private async Task StreamListener(INatsJSConsumer con,
        NatsJSConsumeOpts consumeOpts,
        ConsumerRegistration reg,
        CancellationToken stoppingToken)
    {
        await foreach (var msg in con.ConsumeAsync<byte[]>(opts: consumeOpts, cancellationToken: stoppingToken))
            await _streamChannel!.Writer.WriteAsync((msg, reg), stoppingToken);
    }

    private async Task BroadcastListener(
        ConsumerRegistration reg,
        CancellationToken stoppingToken)
    {
        await foreach
            (var msg in nats.SubscribeAsync<byte[]>(reg.Subject, reg.Stream, cancellationToken: stoppingToken))
            await _broadcastChannel!.Writer.WriteAsync((msg, reg), stoppingToken);
    }

    private async Task BroadcastWorker(CancellationToken stoppingToken)
    {
        await foreach (var tuple in _broadcastChannel!.Reader.ReadAllAsync(stoppingToken))
        {
            var (msg, consumer) = tuple;

            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var consumerInstance = scope.ServiceProvider.GetRequiredService(consumer.Consumer);

                var data = options.Deserialize(msg.Data!, consumer.Type, true);

                dynamic awaitable = consumer.Method.Invoke(consumerInstance, [data, stoppingToken])!;
                await awaitable;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, null);
            }
        }
    }

    private async Task ServiceWorker(CancellationToken stoppingToken)
    {
        await foreach (var tuple in _serviceChannel!.Reader.ReadAllAsync(stoppingToken))
        {
            var (msg, reg) = tuple;

            var invocation = (ServiceInvocation)options.Deserialize(msg.Data!, typeof(ServiceInvocation), true)!;
            var signatures = invocation.Signature.Select(options.ResolveType).ToArray();

            var args = new object[signatures.Length];

            for (var i = 0; i < signatures.Length; i++)
            {
                var sig = signatures[i];
                args[i] = options.Deserialize(invocation.Arguments[i], sig, false);
            }

            await using var scope = serviceProvider.CreateAsyncScope();
            var instance = scope.ServiceProvider.GetRequiredService(reg.ImplementationType);

            try
            {
                var method = reg.Method;

                if (method.IsGenericMethod)
                    method = method.MakeGenericMethod(invocation.Generics.Select(options.ResolveType).ToArray()!);

                dynamic awaitable = method.Invoke(instance, args.ToArray());
                await awaitable;

                if (reg.Method.ReturnType == typeof(Task))
                {
                    await msg.ReplyAsync<byte[]>([]);
                }
                else
                {
                    var res = awaitable.GetAwaiter().GetResult();
                    await msg.ReplyAsync<byte[]>(options.Serialize(res, true));
                }
            }
            catch (Exception e)
            {
                var headers = new NatsHeaders
                {
                    ["exception"] = e.Message
                };

                await msg.ReplyAsync<byte[]>([], headers);
            }
        }
    }

    private async Task StreamWorker(CancellationToken stoppingToken)
    {
        await foreach (var tuple in _streamChannel!.Reader.ReadAllAsync(stoppingToken))
        {
            var (msg, consumer) = tuple;

            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var consumerInstance = scope.ServiceProvider.GetRequiredService(consumer.Consumer);

                var data = options.Deserialize(msg.Data!, consumer.Type, true);

                dynamic awaitable = consumer.Method.Invoke(consumerInstance, [data, stoppingToken])!;
                await awaitable;

                await msg.AckAsync(cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, null);
                await msg.NakAsync(delay: TimeSpan.FromSeconds(30), cancellationToken: stoppingToken);
            }
        }
    }

    private string ApplyPrefix(string a)
    {
        if (options.Prefix != null)
            return $"{options.Prefix}-{a}";
        return a;
    }

    public async Task<T> RequestAsync<T>(MethodInfo info, object[] args)
    {
        var attr = info.DeclaringType!.GetCustomAttribute<ServiceMeshAttribute>()!;

        var subject = ApplyPrefix(options.ResolveService(attr, info));

        var call = new ServiceInvocation
        {
            Service = attr.Name,
            Method = info.Name,
            Signature = args.Select(x => x.GetType().AssemblyQualifiedName!).ToList(),
            Arguments = args.Select(x => options.Serialize(x, false)).ToList(),
            Generics = info.GetGenericArguments().Select(x => x.AssemblyQualifiedName!).ToList()
        };

        var body = options.Serialize(call, true);

        var res = await nats.RequestAsync<byte[], byte[]>(subject,
            body, replyOpts: new NatsSubOpts
            {
                Timeout = TimeSpan.FromSeconds(30)
            });

        res.EnsureSuccess();

        return (T)options.Deserialize(res.Data!, typeof(T), true)!;
    }

    public async Task RequestAsync(MethodInfo info, object[] args)
    {
        var attr = info.DeclaringType!.GetCustomAttribute<ServiceMeshAttribute>()!;
        var subject = ApplyPrefix(options.ResolveService(attr, info));

        var call = new ServiceInvocation
        {
            Service = attr.Name,
            Method = info.Name,
            Signature = args.Select(x => x.GetType().AssemblyQualifiedName!).ToList(),
            Arguments = args.Select(x => options.Serialize(x, false)).ToList(),
            Generics = info.GetGenericArguments().Select(x => x.AssemblyQualifiedName!).ToList()
        };

        var body = options.Serialize(call, true);

        var res = await nats.RequestAsync<byte[], byte[]>(subject,
            body, replyOpts: new NatsSubOpts
            {
                Timeout = TimeSpan.FromSeconds(30)
            });

        res.EnsureSuccess();
    }
}