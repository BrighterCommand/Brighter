using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.MessagingGateway.RocketMQ;

/// <summary>
/// Turns a Brighter message into the RocketMQ message that will be sent for it.
/// </summary>
/// <remarks>
/// The mapping lives on its own type, rather than inside <see cref="RocketMqMessageProducer"/>'s
/// send path, so that what this gateway would put on the wire can be inspected without a broker —
/// the same shape as <c>MqttMessagePublisher.CreateMqttMessage</c>. <c>rocketmq-ci</c> is commented
/// out in <c>ci.yml</c>, so for RocketMQ this is the only route by which anything here is
/// exercised at all.
/// </remarks>
public static class RocketMqMessagePublisher
{
    /// <summary>
    /// Builds the RocketMQ message for <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The Brighter message to send.</param>
    /// <param name="publication">The publication, which supplies the topic, topic type and default tag.</param>
    /// <param name="delay">
    /// How long delivery should be held back, or null for none. Only honoured on a
    /// <see cref="TopicType.Delay"/> topic, or when non-zero.
    /// </param>
    /// <param name="timeProvider">The clock a delivery timestamp is measured from.</param>
    /// <returns>The message to hand to a RocketMQ <c>Producer</c>.</returns>
    public static Org.Apache.Rocketmq.Message CreateRocketMqMessage(
        Message message,
        RocketMqPublication publication,
        TimeSpan? delay,
        TimeProvider timeProvider)
    {
        var builder = new Org.Apache.Rocketmq.Message.Builder()
            .SetBody(message.Body.ToByteArray())
            .SetTopic(publication.Topic!.Value);

        var headerOwnedKeys = AddHeaderProperties(builder, message.Id, message.Header);

        if (publication.TopicType == TopicType.Delay || delay.HasValue && delay.Value != TimeSpan.Zero)
        {
            delay ??= TimeSpan.Zero;
            builder
                .SetDeliveryTimestamp(timeProvider.GetUtcNow().Add(delay.Value).UtcDateTime);
        }

        if (publication.TopicType == TopicType.Fifo || !PartitionKey.IsNullOrEmpty(message.Header.PartitionKey))
        {
            builder.SetMessageGroup(message.Header.PartitionKey.Value);
        }

        foreach (var (key, val) in message.Header.Bag
                     .Where(x => x.Key != HeaderNames.Keys
                                 && x.Key != HeaderNames.Tag
                                 && !MessageHeader.IsLocalHeader(x.Key)
                                 && !headerOwnedKeys.Contains(x.Key)))
        {
            builder.AddProperty(key, val.ToString());
        }

        if (message.Header.Bag.TryGetValue(HeaderNames.Keys, out var keys))
        {
            if (keys is string[] keysArray)
            {
                builder.SetKeys(keysArray);
            }
            else if (keys is string keyString)
            {
                builder.SetKeys(keyString);
            }
        }
        else
        {
            builder.SetKeys(message.Id);
        }

        if (message.Header.Bag.TryGetValue(HeaderNames.Tag, out var tag) && tag is string tagString)
        {
            builder.SetTag(tagString);
        }
        else if (!string.IsNullOrEmpty(publication.Tag))
        {
            builder.SetTag(publication.Tag);
        }

        return builder.Build();
    }

    /// <summary>
    /// Copies <paramref name="header"/> onto <paramref name="builder"/> as RocketMQ properties.
    /// </summary>
    /// <returns>
    /// The property keys this call wrote, so the bag loop in <see cref="CreateRocketMqMessage"/>
    /// can skip them (GCP's <c>!headers.ContainsKey</c> shape, <c>Parser.cs:368</c>) rather than
    /// overwrite them with a same-named, possibly stale, bag entry - RocketMQ's
    /// <see cref="Org.Apache.Rocketmq.Message.Builder.AddProperty"/> is last-write-wins.
    /// </returns>
    /// <remarks>
    /// RocketMQ's <c>AddProperty</c> rejects an empty value with <see cref="ArgumentException"/>,
    /// so every optional header has to be checked before it is written - a header that simply is
    /// not set would otherwise fail the send rather than be omitted.
    /// </remarks>
    private static HashSet<string> AddHeaderProperties(
        Org.Apache.Rocketmq.Message.Builder builder, Id messageId, MessageHeader header)
    {
        var writtenKeys = new HashSet<string>(StringComparer.Ordinal);

        void Add(string key, string? value)
        {
            builder.AddProperty(key, value);
            writtenKeys.Add(key);
        }

        Add(HeaderNames.MessageId, messageId);
        Add(HeaderNames.Topic, header.Topic.Value);
        Add(HeaderNames.HandledCount, header.HandledCount.ToString());
#pragma warning disable CS0618 // Preserve the legacy message type for transport compatibility.
        Add(HeaderNames.MessageType, header.MessageType.ToString());
#pragma warning restore CS0618
        Add(HeaderNames.TimeStamp, header.TimeStamp.ToRfc3339());
        Add(HeaderNames.Source, header.Source.ToString());
        Add(HeaderNames.SpecVersion, header.SpecVersion);

        var baggage = header.Baggage.ToString();
        if (!string.IsNullOrEmpty(baggage))
        {
            Add(HeaderNames.Baggage, baggage);
        }

        if (header.Type != CloudEventsType.Empty)
        {
            Add(HeaderNames.Type, header.Type);
        }

        if (!string.IsNullOrEmpty(header.Subject))
        {
            Add(HeaderNames.Subject, header.Subject);
        }

        if (header.DataSchema != null)
        {
            Add(HeaderNames.DataSchema, header.DataSchema.ToString());
        }

        Add(HeaderNames.ContentType, header.ContentType.ToString());
        Add(HeaderNames.DataContentType, header.ContentType.ToString());

        if (!string.IsNullOrEmpty(header.CorrelationId))
        {
            Add(HeaderNames.CorrelationId, header.CorrelationId);
        }

        if (!RoutingKey.IsNullOrEmpty(header.ReplyTo))
        {
            Add(HeaderNames.ReplyTo, header.ReplyTo);
        }

        if (!string.IsNullOrEmpty(header.DataRef))
        {
            Add(HeaderNames.DataRef, header.DataRef);
        }

        if (!TraceParent.IsNullOrEmpty(header.TraceParent))
        {
            Add(HeaderNames.TraceParent, header.TraceParent.Value);
        }

        if (!TraceState.IsNullOrEmpty(header.TraceState))
        {
            Add(HeaderNames.TraceState, header.TraceState.Value);
        }

        return writtenKeys;
    }
}
