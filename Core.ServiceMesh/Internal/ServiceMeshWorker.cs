﻿using System.Diagnostics;
using System.Reflection;
using System.Threading.Channels;
using Core.ServiceMesh.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

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
    private IBlobProvider? _blobStorage = serviceProvider.GetService<IBlobProvider>();

    public T CreateProxy<T>() where T : class
    {
        var remoteProxy = typeof(T).Assembly.GetType(typeof(T).FullName + "RemoteProxy");

        if (remoteProxy == null)
            throw new InvalidOperationException($"no proxy was found for interface {typeof(T).FullName}");

        return (T)Activator.CreateInstance(remoteProxy, this)!;
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

        using var activity = ServiceMeshActivity.Source.StartActivity($"PUB {message.GetType().Name}",
            ActivityKind.Producer, Activity.Current?.Context ?? default);

        //if (activity != null)
        //    activity.DisplayName = subject;

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

        using var activity = ServiceMeshActivity.Source.StartActivity($"PUB {message.GetType().Name}",
            ActivityKind.Producer, Activity.Current?.Context ?? default);

        // if (activity != null)
        //     activity.DisplayName = subject;

        await nats.PublishAsync(subject, data, headers);
    }

    public async ValueTask<T> RequestAsync<T>(string subject, object?[] args, Type[] generics)
    {
        subject = ApplyPrefix(subject);

        var call = new ServiceInvocation
        {
            //Service = attr.Name,
            //Method = info.Name,
            Signature = args.Select(x => x?.GetType().AssemblyQualifiedName).ToList(),
            Arguments = args.Select(x => x != null ? options.Serialize(x, false) : null).ToList(),
            Generics = generics.Select(x => x.AssemblyQualifiedName!).ToList()
        };

        var body = options.Serialize(call, true);

        var headers = new NatsHeaders();

        var activityContext = Activity.Current?.Context ?? default;
        Propagators.DefaultTextMapPropagator.Inject(
            new PropagationContext(activityContext, Baggage.Current), headers,
            (h, key, value) => { h[key] = value; });

        using var activity =
            ServiceMeshActivity.Source.StartActivity($"REQ {subject}", ActivityKind.Client, activityContext);

        //if (activity != null)
        //    activity.DisplayName = $"{call.Service}.{call.Method}";

        var res = await nats.RequestAsync<byte[], byte[]>(subject,
            body, replyOpts: new NatsSubOpts
            {
                //Timeout = TimeSpan.FromSeconds(30)
            }, headers: headers);

        res.EnsureSuccess();

        return (T)options.Deserialize(res.Data!, typeof(T), true)!;
    }

    public async ValueTask RequestAsync(string subject, object?[] args, Type[] generics)
    {
        subject = ApplyPrefix(subject);

        var call = new ServiceInvocation
        {
            //Service = attr.Name,
            //Method = info.Name,
            Signature = args.Select(x => x?.GetType().AssemblyQualifiedName).ToList(),
            Arguments = args.Select(x => x != null ? options.Serialize(x, false) : null).ToList(),
            Generics = generics.Select(x => x.AssemblyQualifiedName!).ToList()
        };

        var body = options.Serialize(call, true);

        var headers = new NatsHeaders();

        var activityContext = Activity.Current?.Context ?? default;

        Propagators.DefaultTextMapPropagator.Inject(
            new PropagationContext(activityContext, Baggage.Current), headers,
            (h, key, value) => { h[key] = value; });

        using var activity =
            ServiceMeshActivity.Source.StartActivity($"REQ {subject}", ActivityKind.Client, activityContext);

        //if (activity != null)
        //    activity.DisplayName = $"";

        var res = await nats.RequestAsync<byte[], byte[]>(subject,
            body, replyOpts: new NatsSubOpts
            {
                //Timeout = TimeSpan.FromSeconds(30)
            }, headers: headers);

        res.EnsureSuccess();
    }

    public async IAsyncEnumerable<T> StreamAsync<T>(string subject, object?[] args, Type[] generics)
    {
        subject = ApplyPrefix(subject);

        var call = new ServiceInvocation
        {
            //Service = attr.Name,
            //Method = info.Name,
            Signature = args.Select(x => x?.GetType().AssemblyQualifiedName).ToList(),
            Arguments = args.Select(x => x != null ? options.Serialize(x, false) : null).ToList(),
            Generics = generics.Select(x => x.AssemblyQualifiedName!).ToList()
        };

        var body = options.Serialize(call, true);

        var headers = new NatsHeaders();

        var activityContext = Activity.Current?.Context ?? default;
        Propagators.DefaultTextMapPropagator.Inject(
            new PropagationContext(activityContext, Baggage.Current), headers,
            (h, key, value) => { h[key] = value; });

        using var activity =
            ServiceMeshActivity.Source.StartActivity($"REQ {subject}", ActivityKind.Client, activityContext);

        var subId = Guid.NewGuid().ToString("N");

        headers["return-sub-id"] = subId;
        await nats.PublishAsync(subject, body, headers);

        await foreach (var msg in nats.SubscribeAsync<byte[]>(subId))
        {
            if (msg.Data == null)
                yield break;
            yield return (T)options.Deserialize(msg.Data!, typeof(T), true)!;
        }
    }

    public ValueTask<BlobRef> UploadBlobAsync(Stream readStream, string contentType, TimeSpan? expire,
        CancellationToken cancellationToken = default)
    {
        if (_blobStorage == null)
            throw new InvalidOperationException("blob storage not configured");

        return _blobStorage.UploadBlobAsync(readStream, contentType, expire, cancellationToken);
    }

    public ValueTask DownloadBlobAsync(BlobRef blob, Stream writeStream, CancellationToken cancellationToken = default)
    {
        if (_blobStorage == null)
            throw new InvalidOperationException("blob storage not configured");

        return _blobStorage.DownloadBlobAsync(blob, writeStream, cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.DeveloperMode)
            return;

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

                var oldcfg = streamInfo.Info.Config;
                var mergedSubjects = (oldcfg.Subjects ?? []).ToHashSet();
                mergedSubjects.UnionWith(subjects);

                var newcfg = new StreamConfig(stream.Key, mergedSubjects.ToList());
                options.ConfigureStream(stream.Key, newcfg);

                if (newcfg.Storage != oldcfg.Storage
                    || newcfg.Name != oldcfg.Name
                    || (newcfg.MaxConsumers != oldcfg.MaxConsumers && newcfg.MaxConsumers > 0)
                    || newcfg.Retention != oldcfg.Retention
                    || newcfg.DenyDelete != oldcfg.DenyDelete
                    || newcfg.DenyPurge != oldcfg.DenyPurge
                   )
                {
                    logger.LogError("stream {0} cannot be updated", stream.Key);
                    continue;
                }

                if (!mergedSubjects.SetEquals(subjects)
                    || newcfg.MaxAge != oldcfg.MaxAge
                    || newcfg.MaxMsgs != oldcfg.MaxMsgs
                    || newcfg.MaxBytes != oldcfg.MaxBytes
                    || newcfg.DuplicateWindow != oldcfg.DuplicateWindow
                    || newcfg.NumReplicas != oldcfg.NumReplicas
                    || newcfg.MaxMsgSize != oldcfg.MaxMsgSize
                    || newcfg.NoAck != oldcfg.NoAck
                    || newcfg.Discard != oldcfg.Discard
                    || newcfg.Placement?.Cluster != oldcfg.Placement?.Cluster
                    || !new HashSet<string>(newcfg.Placement?.Tags ?? []).SetEquals(oldcfg.Placement?.Tags ?? [])
                    || newcfg.MaxMsgsPerSubject != oldcfg.MaxMsgsPerSubject
                    || newcfg.AllowRollupHdrs != oldcfg.AllowRollupHdrs
                    || newcfg.Republish != oldcfg.Republish
                    || newcfg.Compression != oldcfg.Compression
                   )
                    await _jetStream.UpdateStreamAsync(newcfg, stoppingToken);
            }
            catch (Exception)
            {
                var newStreamConfig = new StreamConfig(stream.Key, subjects)
                {
                    Storage = StreamConfigStorage.File,
                    DuplicateWindow = TimeSpan.FromMinutes(10),
                    MaxAge = TimeSpan.FromDays(14)
                };

                options.ConfigureStream(stream.Key, newStreamConfig);

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
                    Backoff = durableConsumer.Durable!.Backoff.Any() ? 
                        durableConsumer.Durable!.Backoff.Select(TimeSpan.FromSeconds).ToArray() 
                        : null,
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

                var modifiedConsumeOpts =
                    options.ConfigureConsumer(durableConsumer.Name, newConsumerConfig, consumeOpts);

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
        await foreach (var msg in nats.SubscribeAsync<byte[]>(reg.Sub + ".>", reg.QueueGroup,
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
        await foreach
            (var msg in nats.SubscribeAsync<byte[]>(subject, reg.QueueGroup, cancellationToken: stoppingToken))
            await _broadcastChannel!.Writer.WriteAsync((msg, reg), stoppingToken);
    }

    private async Task TransientWorker(CancellationToken stoppingToken)
    {
        await foreach (var tuple in _broadcastChannel!.Reader.ReadAllAsync(stoppingToken))
        {
            var (msg, consumer) = tuple;

            var parentContext = Propagators.DefaultTextMapPropagator.Extract(default, msg.Headers, (headers, key) =>
            {
                if (headers!.TryGetValue(key, out var value))
                    return [value[0]!];

                return Array.Empty<string>();
            });
            Baggage.Current = parentContext.Baggage;

            using var activity =
                ServiceMeshActivity.Source.StartActivity("NATS", ActivityKind.Consumer, parentContext.ActivityContext);

            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var consumerInstance = scope.ServiceProvider.GetRequiredService(consumer.Consumer);

                if (consumer.Methods.TryGetValue(msg.Subject, out var info))
                {
                    if (activity != null)
                        activity.DisplayName = $"SUB {info.MessageType.Name}";

                    var data = options.Deserialize(msg.Data!, info.MessageType, true);

                    dynamic awaitable = info.Method.Invoke(consumerInstance, [data, stoppingToken])!;
                    await awaitable;
                    activity?.SetStatus(ActivityStatusCode.Ok);
                }
                else
                {
                    activity?.SetStatus(ActivityStatusCode.Error);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, null);
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.AddException(ex);
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
                if (headers!.TryGetValue(key, out var value))
                    return [value[0]!];

                return Array.Empty<string>();
            });
            Baggage.Current = parentContext.Baggage;

            using var activity =
                ServiceMeshActivity.Source.StartActivity("NATS", ActivityKind.Server, parentContext.ActivityContext);

            if (!reg.Methods.TryGetValue(msg.Subject, out var method))
            {
                var headers = new NatsHeaders
                {
                    ["exception"] = $"method handler for {msg.Subject} not found"
                };

                await msg.ReplyAsync<byte[]>([], headers);
                activity?.SetStatus(ActivityStatusCode.Error);
            }
            else
            {
                var invocation = (ServiceInvocation)options.Deserialize(msg.Data!, typeof(ServiceInvocation), true)!;
                var signatures = invocation.Signature.Select(x=> x == null ? null : options.ResolveType(x)).ToArray();

                if (activity != null)
                    activity.DisplayName = $"SUB {msg.Subject}";

                var args = new object?[signatures.Length];

                for (var i = 0; i < signatures.Length; i++)
                {
                    var sig = signatures[i]!;
                    
                    if (sig != null!)
                        args[i] = options.Deserialize(invocation.Arguments[i]!, sig, false)!;
                }

                await using var scope = serviceProvider.CreateAsyncScope();
                var instance = scope.ServiceProvider.GetRequiredService(reg.ImplementationType);

                if (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
                {
                    var subId = msg.Headers!["return-sub-id"].FirstOrDefault();

                    try
                    {
                        var resType = method.ReturnType.GenericTypeArguments[0];

                        var wrapper = typeof(ServiceMeshWorker).GetMethod(nameof(AsyncEnumerableWrapper),
                                BindingFlags.NonPublic | BindingFlags.Instance)!
                            .MakeGenericMethod(resType);

                        dynamic stream = method.Invoke(instance, args.ToArray())!;

                        dynamic awaitable = wrapper.Invoke(this, [subId, stream])!;
                        await awaitable;
                    }
                    catch (Exception ex)
                    {
                        var headers = new NatsHeaders
                        {
                            ["exception"] = ex.Message
                        };

                        await nats.PublishAsync<byte[]>(subId!, [], headers);
                        activity?.SetStatus(ActivityStatusCode.Error);
                        activity?.AddException(ex);
                    }
                }
                else
                {
                    try
                    {
                        if (method.IsGenericMethod)
                            method = method.MakeGenericMethod(
                                invocation.Generics.Select(options.ResolveType).ToArray()!);

                        dynamic awaitable = method.Invoke(instance, args.ToArray())!;
                        await awaitable;

                        if (method.ReturnType == typeof(Task) || method.ReturnType == typeof(ValueTask))
                        {
                            await msg.ReplyAsync<byte[]>([]);
                            activity?.SetStatus(ActivityStatusCode.Ok);
                        }
                        else
                        {
                            var res = awaitable.GetAwaiter().GetResult();
                            await msg.ReplyAsync<byte[]>(options.Serialize(res, true));
                            activity?.SetStatus(ActivityStatusCode.Ok);
                        }
                    }
                    catch (Exception ex)
                    {
                        var headers = new NatsHeaders
                        {
                            ["exception"] = ex.Message
                        };

                        await msg.ReplyAsync<byte[]>([], headers);
                        activity?.SetStatus(ActivityStatusCode.Error);
                        activity?.AddException(ex);
                    }
                }
            }
        }
    }

    private async ValueTask AsyncEnumerableWrapper<T>(string sub, IAsyncEnumerable<T> stream)
    {
        await foreach (var val in stream)
        {
            var data = options.Serialize(val!, true);
            await nats.PublishAsync(sub, data);
        }

        await nats.PublishAsync(sub, Array.Empty<byte>());
    }

    private async Task DurableWorker(CancellationToken stoppingToken)
    {
        await foreach (var tuple in _streamChannel!.Reader.ReadAllAsync(stoppingToken))
        {
            var (msg, consumer) = tuple;

            var parentContext = Propagators.DefaultTextMapPropagator.Extract(default, msg.Headers, (headers, key) =>
            {
                if (headers!.TryGetValue(key, out var value))
                    return [value[0]!];

                return Array.Empty<string>();
            });
            Baggage.Current = parentContext.Baggage;

            using var activity =
                ServiceMeshActivity.Source.StartActivity("NATS", ActivityKind.Consumer, parentContext.ActivityContext);

            activity?.SetTag("nats.delivered", msg.Metadata?.NumDelivered);
            activity?.SetTag("nats.pending", msg.Metadata?.NumPending);
            activity?.SetTag("nats.consumer", msg.Metadata?.Consumer);

            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var consumerInstance = scope.ServiceProvider.GetRequiredService(consumer.Consumer);

                if (consumer.Methods.TryGetValue(msg.Subject, out var info))
                {
                    if (activity != null)
                        activity.DisplayName = $"STREAM {info.MessageType.Name}";

                    var data = options.Deserialize(msg.Data!, info.MessageType, true);

                    dynamic awaitable = info.Method.Invoke(consumerInstance, [data, stoppingToken])!;
                    await awaitable;
                    await msg.AckAsync(cancellationToken: stoppingToken);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                }
                else
                {
                    await msg.NakAsync(cancellationToken: stoppingToken);
                }
            }
            catch (Exception ex)
            {
                await msg.NakAsync(cancellationToken: stoppingToken);
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.AddException(ex);
                logger.LogError(ex, null);
            }
        }
    }

    private string ApplyPrefix(string a)
    {
        if (options.Prefix != null)
            return $"{options.Prefix}-{a}";
        return a;
    }

    public ValueTask<BlobRef> UploadBlob(Stream readStream, TimeSpan? expire)
    {
        throw new NotImplementedException();
    }

    public ValueTask DownloadBlob(BlobRef blob, Stream writeStream)
    {
        throw new NotImplementedException();
    }
}