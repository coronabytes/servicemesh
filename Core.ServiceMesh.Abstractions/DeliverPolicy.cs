namespace Core.ServiceMesh.Abstractions;

public enum DeliverPolicy
{
    /// <summary>
    ///     Default policy. Start receiving from the earliest available message in the stream.
    /// </summary>
    All,

    /// <summary>
    ///     Start with the last message added to the stream, or the last message matching the consumer's filter subject if
    ///     defined.
    /// </summary>
    Last,

    /// <summary>
    ///     Start with the latest message for each filtered subject currently in the stream.
    /// </summary>
    LastPerSubject,

    /// <summary>
    ///     Start receiving messages created after the consumer was created.
    /// </summary>
    New

    /*/// <summary>
    ///   Start at the first message with the specified sequence number. The consumer must specify OptStartSeq defining the sequence number.
    /// </summary>
    DeliverByStartSequence,

    /// <summary>
    ///   Start with messages on or after the specified time. The consumer must specify OptStartTime defining the start time.
    /// </summary>
    DeliverByStartTime*/
}