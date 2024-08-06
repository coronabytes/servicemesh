using System.Diagnostics;
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
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Core.ServiceMesh.Internal;

internal class ServiceMeshWorker(
    NatsConnection nats,
    IServiceProvider serviceProvider,
    ILogger<ServiceMeshWorker> logger,
    ServiceMeshOptions options) : BackgroundService, IServiceMesh
{
    internal static readonly ActivitySource ActivitySource = new("core.servicemesh");
    private readonly NatsJSContext _jetStream = new(nats);
    private Channel<(NatsMsg<byte[]>, ConsumerRegistration)>? _broadcastChannel;
    private Channel<(NatsMsg<byte[]>, ServiceRegistration)>? _serviceChannel;
    private Channel<(NatsJSMsg<byte[]>, ConsumerRegistration)>? _streamChannel;

    public T CreateProxy<T>() where T : class
    {
        return DispatchProxyAsync.Create<T, RemoteDispatchProxy>();
    }

    public async ValueTask PublishAsync(object message,
        int retry = 3,
        TimeSpan? retryWait = null, string? id = null)
    {
        var subject = ApplyPrefix(options.ResolveSubject(message.GetType()));
        var data = options.Serialize(message, true);

        var headers = new NatsHeaders();

        if (Activity.Current != null)
            Propagators.DefaultTextMapPropagator.Inject(
                new PropagationContext(Activity.Current.Context, Baggage.Current), headers,
                (h, key, value) => { h[key] = value; });

        using var activity = ActivitySource.StartActivity("NATS", ActivityKind.Producer, Activity.Current.Context);

        if (activity != null)
            activity.DisplayName = subject;

        var res = await _jetStream.PublishAsync(subject, data, opts: new NatsJSPubOpts
        {
            MsgId = id ?? Guid.NewGuid().ToString("N"),
            RetryAttempts = retry,
            RetryWaitBetweenAttempts = retryWait ?? TimeSpan.FromSeconds(30)
        }, headers: headers);

        res.EnsureSuccess();
    }

    public async ValueTask SendAsync(object message)
    {
        var subject = ApplyPrefix(options.ResolveSubject(message.GetType()));
        var data = options.Serialize(message, true);

        var headers = new NatsHeaders();

        if (Activity.Current != null)
            Propagators.DefaultTextMapPropagator.Inject(
                new PropagationContext(Activity.Current.Context, Baggage.Current), headers,
                (h, key, value) => { h[key] = value; });

        using var activity = ActivitySource.StartActivity("NATS", ActivityKind.Producer, Activity.Current.Context);

        if (activity != null)
            activity.DisplayName = subject;

        await nats.PublishAsync(subject, data, headers);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RemoteDispatchProxy.Worker = this;

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
            var subjects = stream.SelectMany(x => x.Subjects).Distinct().ToList();

            try
            {
                var streamInfo = await _jetStream.GetStreamAsync(stream.Key);

                var currentStreamConfig = streamInfo.Info.Config;
                var mergedSubjects = (currentStreamConfig.Subjects ?? []).ToHashSet();
                mergedSubjects.UnionWith(subjects);

                var newStreamConfig = new StreamConfig(stream.Key, mergedSubjects.ToList());
                options.ConfigureStream(stream.Key, newStreamConfig);

                if (!mergedSubjects.SetEquals(subjects))
                    await _jetStream.UpdateStreamAsync(newStreamConfig, stoppingToken);
            }
            catch (Exception)
            {
                var newStreamConfig = new StreamConfig(stream.Key, subjects)
                {
                    Storage = StreamConfigStorage.File,
                    DuplicateWindow = TimeSpan.FromMinutes(10),
                    MaxAge = TimeSpan.FromDays(14)
                };

                await _jetStream.CreateStreamAsync(newStreamConfig, stoppingToken);
            }
        }


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
                var newConsumerConfig = new ConsumerConfig
                {
                    Name = durableConsumer.Name,
                    DurableName = durableConsumer.Name,
                    AckPolicy = ConsumerConfigAckPolicy.Explicit,
                    MaxDeliver = durableConsumer.Durable!.MaxDeliver,
                    MaxAckPending = durableConsumer.Durable!.MaxAckPending,
                    AckWait = TimeSpan.FromSeconds(durableConsumer.Durable!.AckWait),
                    FilterSubjects = durableConsumer.Subjects,
                    // TODO: check if seconds or nanoseconds
                    Backoff = durableConsumer.Durable!.Backoff.Any() ? durableConsumer.Durable!.Backoff : null,
                    DeliverPolicy = durableConsumer.Durable!.DeliverPolicy switch
                    {
                        DeliverPolicy.All => ConsumerConfigDeliverPolicy.All,
                        DeliverPolicy.Last => ConsumerConfigDeliverPolicy.Last,
                        DeliverPolicy.LastPerSubject => ConsumerConfigDeliverPolicy.LastPerSubject,
                        DeliverPolicy.New => ConsumerConfigDeliverPolicy.New,
                        _ => ConsumerConfigDeliverPolicy.All
                    }
                };

                var consumeOpts = new NatsJSConsumeOpts
                {
                    MaxMsgs = 10,
                    MaxBytes = null,
                    Expires = TimeSpan.FromSeconds(10),
                    IdleHeartbeat = TimeSpan.FromSeconds(5)
                };

                var modifiedConsumeOpts = options.ConfigureConsumer(durableConsumer.Name, newConsumerConfig, consumeOpts);

                var con = await _jetStream
                    .CreateOrUpdateConsumerAsync(durableConsumer.Stream, newConsumerConfig, stoppingToken)
                    .ConfigureAwait(false);

                tasks.Add(DurableListener(con, modifiedConsumeOpts, durableConsumer, stoppingToken));
            }

        foreach (var transientConsumer in ServiceMeshExtensions.Consumers.Where(x => !x.IsDurable))
            foreach (var m in transientConsumer.Methods)
                tasks.Add(TransientListener(transientConsumer, m.Key, stoppingToken));

        for (var i = 0; i < options.StreamWorkers; i++)
            tasks.Add(DurableWorker(stoppingToken));
        for (var i = 0; i < options.ServiceWorkers; i++)
            tasks.Add(ServiceWorker(stoppingToken));
        for (var i = 0; i < options.BroadcastWorkers; i++)
            tasks.Add(TransientWorker(stoppingToken));

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

    private async Task DurableListener(INatsJSConsumer con,
        NatsJSConsumeOpts consumeOpts,
        ConsumerRegistration reg,
        CancellationToken stoppingToken)
    {
        await foreach (var msg in con.ConsumeAsync<byte[]>(opts: consumeOpts, cancellationToken: stoppingToken))
            await _streamChannel!.Writer.WriteAsync((msg, reg), stoppingToken);
    }

    private async Task TransientListener(
        ConsumerRegistration reg,
        string subject,
        CancellationToken stoppingToken)
    {
        await foreach (var msg in nats.SubscribeAsync<byte[]>(subject, reg.QueueGroup, cancellationToken: stoppingToken))
            await _broadcastChannel!.Writer.WriteAsync((msg, reg), stoppingToken);
    }

    private async Task TransientWorker(CancellationToken stoppingToken)
    {
        await foreach (var tuple in _broadcastChannel!.Reader.ReadAllAsync(stoppingToken))
        {
            var (msg, consumer) = tuple;

            var parentContext = Propagators.DefaultTextMapPropagator.Extract(default, msg.Headers, (headers, key) =>
            {
                if (headers.TryGetValue(key, out var value))
                    return [value[0]];

                return Array.Empty<string>();
            });
            Baggage.Current = parentContext.Baggage;

            using var activity =
                ActivitySource.StartActivity("NATS", ActivityKind.Consumer, parentContext.ActivityContext);

            if (activity != null)
                activity.DisplayName = msg.Subject;

            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var consumerInstance = scope.ServiceProvider.GetRequiredService(consumer.Consumer);

                if (consumer.Methods.TryGetValue(msg.Subject, out var info))
                {
                    var data = options.Deserialize(msg.Data!, info.MessageType, true);

                    dynamic awaitable = info.Method.Invoke(consumerInstance, [data, stoppingToken])!;
                    await awaitable;
                }
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

            var parentContext = Propagators.DefaultTextMapPropagator.Extract(default, msg.Headers, (headers, key) =>
            {
                if (headers.TryGetValue(key, out var value))
                    return [value[0]];

                return Array.Empty<string>();
            });
            Baggage.Current = parentContext.Baggage;

            using var activity =
                ActivitySource.StartActivity("NATS", ActivityKind.Server, parentContext.ActivityContext);

            var invocation = (ServiceInvocation)options.Deserialize(msg.Data!, typeof(ServiceInvocation), true)!;
            var signatures = invocation.Signature.Select(options.ResolveType).ToArray();

            if (activity != null)
                activity.DisplayName = $"{invocation.Service}.{invocation.Method}";

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

    private async Task DurableWorker(CancellationToken stoppingToken)
    {
        await foreach (var tuple in _streamChannel!.Reader.ReadAllAsync(stoppingToken))
        {
            var (msg, consumer) = tuple;

            var parentContext = Propagators.DefaultTextMapPropagator.Extract(default, msg.Headers, (headers, key) =>
            {
                if (headers.TryGetValue(key, out var value))
                    return [value[0]];

                return Array.Empty<string>();
            });
            Baggage.Current = parentContext.Baggage;

            using var activity =
                ActivitySource.StartActivity("NATS", ActivityKind.Consumer, parentContext.ActivityContext);

            if (activity != null)
                activity.DisplayName = msg.Subject;

            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var consumerInstance = scope.ServiceProvider.GetRequiredService(consumer.Consumer);

                if (consumer.Methods.TryGetValue(msg.Subject, out var info))
                {
                    var data = options.Deserialize(msg.Data!, info.MessageType, true);

                    dynamic awaitable = info.Method.Invoke(consumerInstance, [data, stoppingToken])!;
                    await awaitable;
                    await msg.AckAsync(cancellationToken: stoppingToken);
                }
                else
                {
                    await msg.NakAsync(cancellationToken: stoppingToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, null);
                await msg.NakAsync(cancellationToken: stoppingToken);
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

        var headers = new NatsHeaders();

        var activityContext = Activity.Current?.Context ?? default;
        Propagators.DefaultTextMapPropagator.Inject(
            new PropagationContext(activityContext, Baggage.Current), headers,
            (h, key, value) => { h[key] = value; });

        using var activity = ActivitySource.StartActivity("NATS", ActivityKind.Client, activityContext);

        if (activity != null)
            activity.DisplayName = $"{call.Service}.{call.Method}";

        var res = await nats.RequestAsync<byte[], byte[]>(subject,
            body, replyOpts: new NatsSubOpts
            {
                Timeout = TimeSpan.FromSeconds(30)
            }, headers: headers);

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

        var headers = new NatsHeaders();

        var activityContext = Activity.Current?.Context ?? default;

        Propagators.DefaultTextMapPropagator.Inject(
            new PropagationContext(activityContext, Baggage.Current), headers,
            (h, key, value) => { h[key] = value; });

        using var activity = ActivitySource.StartActivity("NATS", ActivityKind.Client, activityContext);

        if (activity != null)
            activity.DisplayName = $"{call.Service}.{call.Method}";

        var res = await nats.RequestAsync<byte[], byte[]>(subject,
            body, replyOpts: new NatsSubOpts
            {
                Timeout = TimeSpan.FromSeconds(30)
            }, headers: headers);

        res.EnsureSuccess();
    }
}