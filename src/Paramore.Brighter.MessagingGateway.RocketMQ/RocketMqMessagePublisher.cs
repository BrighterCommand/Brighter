using System;
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

        AddHeaderProperties(builder, message.Id, message.Header);

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
                                 && !MessageHeader.IsLocalHeader(x.Key)))
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
    /// <remarks>
    /// RocketMQ's <c>AddProperty</c> rejects an empty value with <see cref="ArgumentException"/>,
    /// so every optional header has to be checked before it is written - a header that simply is
    /// not set would otherwise fail the send rather than be omitted.
    /// </remarks>
    private static void AddHeaderProperties(
        Org.Apache.Rocketmq.Message.Builder builder, Id messageId, MessageHeader header)
    {
        builder.AddProperty(HeaderNames.MessageId, messageId)
            .AddProperty(HeaderNames.Topic, header.Topic.Value)
            .AddProperty(HeaderNames.HandledCount, header.HandledCount.ToString())
            .AddProperty(HeaderNames.MessageType, header.MessageType.ToString())
            .AddProperty(HeaderNames.TimeStamp, header.TimeStamp.ToRfc3339())
            .AddProperty(HeaderNames.Source, header.Source.ToString())
            .AddProperty(HeaderNames.SpecVersion, header.SpecVersion);

        var baggage = header.Baggage.ToString();
        if (!string.IsNullOrEmpty(baggage))
        {
            builder.AddProperty(HeaderNames.Baggage, baggage);
        }

        if (header.Type != CloudEventsType.Empty)
        {
            builder.AddProperty(HeaderNames.Type, header.Type);
        }

        if (!string.IsNullOrEmpty(header.Subject))
        {
            builder.AddProperty(HeaderNames.Subject, header.Subject);
        }

        if (header.DataSchema != null)
        {
            builder.AddProperty(HeaderNames.DataSchema, header.DataSchema.ToString());
        }

        builder.AddProperty(HeaderNames.ContentType, header.ContentType.ToString());
        builder.AddProperty(HeaderNames.DataContentType, header.ContentType.ToString());

        if (!string.IsNullOrEmpty(header.CorrelationId))
        {
            builder.AddProperty(HeaderNames.CorrelationId, header.CorrelationId);
        }

        if (!RoutingKey.IsNullOrEmpty(header.ReplyTo))
        {
            builder.AddProperty(HeaderNames.ReplyTo, header.ReplyTo);
        }

        if (!string.IsNullOrEmpty(header.DataRef))
        {
            builder.AddProperty(HeaderNames.DataRef, header.DataRef);
        }

        if (!TraceParent.IsNullOrEmpty(header.TraceParent))
        {
            builder.AddProperty(HeaderNames.TraceParent, header.TraceParent.Value);
        }

        if (!TraceState.IsNullOrEmpty(header.TraceState))
        {
            builder.AddProperty(HeaderNames.TraceState, header.TraceState.Value);
        }
    }
}
