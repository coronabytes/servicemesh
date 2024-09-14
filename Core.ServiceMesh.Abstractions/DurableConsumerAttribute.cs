namespace Core.ServiceMesh.Abstractions;

[AttributeUsage(AttributeTargets.Class)]
public class DurableConsumerAttribute(string name) : Attribute
{
    /// <summary>
    ///     Unique name for the durable consumer
    /// </summary>
    public string Name => name;

    /// <summary>
    ///     Stream name to subscribe to. Null means container default stream
    /// </summary>
    public string? Stream { get; init; }

    /// <summary>
    ///     The maximum number of times a specific message delivery will be attempted. Applies to any message that is re-sent
    ///     due to acknowledgment policy (i.e., due to a negative acknowledgment or no acknowledgment sent by the client). The
    ///     default is -1 (redeliver until acknowledged). Messages that have reached the maximum delivery count will stay in
    ///     the stream.
    /// </summary>
    public long MaxDeliver { get; init; } = -1;

    /// <summary>
    ///     Defines the maximum number of messages, without acknowledgment, that can be outstanding. Once this limit is
    ///     reached, message delivery will be suspended. This limit applies across all of the consumer's bound subscriptions. A
    ///     value of -1 means there can be any number of pending acknowledgments (i.e., no flow control). The default is 1000.
    /// </summary>
    public long MaxAckPending { get; init; } = 1000;

    /// <summary>
    ///     The duration (in seconds) that the server will wait for an acknowledgment for any individual message once it has
    ///     been delivered to a consumer. If an acknowledgment is not received in time, the message will be redelivered.
    /// </summary>
    public long AckWait { get; set; } = 60 * 5;

    /// <summary>
    ///     The point in the stream from which to receive messages
    ///     Cannot be updated.
    /// </summary>
    public DeliverPolicy DeliverPolicy { get; init; } = DeliverPolicy.All;

    /// <summary>
    ///     A sequence of durations (in seconds) controlling the redelivery of messages on nak or acknowledgment timeout.
    ///     Overrides ackWait. The sequence length must be less than or equal to MaxDelivery. If backoff is not set, a nak will
    ///     result in immediate redelivery.
    /// </summary>
    public long[] Backoff { get; init; } = [];
}