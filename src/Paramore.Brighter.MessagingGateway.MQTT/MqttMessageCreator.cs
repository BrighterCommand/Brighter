using System;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.MQTT
{
    /// <summary>
    /// Turns an MQTT payload into a Brighter message, or reports that it could not be read.
    /// </summary>
    /// <remarks>
    /// The inverse of <see cref="MqttMessagePublisher.CreateMqttMessage"/>, and the MQTT
    /// counterpart of <c>KafkaMessageCreator</c> and <c>RmqMessageCreator</c>: the mapping between
    /// the wire form and <see cref="Message"/> lives on its own type rather than inside the
    /// consumer, so it can be exercised — by us or by a caller — without a broker.
    /// </remarks>
    public static partial class MqttMessageCreator
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MqttMessageConsumer>();

        /// <summary>
        /// Turns an arriving MQTT payload into a <see cref="Message"/>, or reports that it could
        /// not be read.
        /// </summary>
        /// <remarks>
        /// The payload is whatever a publisher put on the topic, so neither failure is
        /// exceptional: a literal <c>null</c> document deserialises to <c>null</c>, and a
        /// malformed one throws. Both are dropped here rather than allowed out. A <c>null</c>
        /// written into the consumer's channel reaches the pump, which dereferences
        /// <see cref="Message.Header"/> and fails on an unrelated thread; a throw escapes into
        /// MQTTnet's dispatch loop rather than into any caller. Either would let one poison
        /// message published by anybody on the topic stop the consumer.
        /// <para>
        /// The catch is deliberately every exception, not a list of types. A malformed document
        /// does fault as <see cref="JsonException"/>, because <c>Utf8JsonReader</c> marks its own
        /// faults for <c>JsonSerializer</c> to rewrap — but a field whose value is merely illegal
        /// need not. <c>MessageHeader.ContentType</c> is a <c>System.Net.Mime.ContentType</c> with
        /// no registered converter, so it is populated as a POCO through its own setters, and those
        /// are user code: an unparseable media type surfaces as <c>FormatException</c>, an empty
        /// one as <c>ArgumentException</c>, and an explicit <c>"contentType": null</c> as
        /// <c>NullReferenceException</c> — three types from one field. Since the block wraps a
        /// single statement whose whole job is to parse untrusted bytes, any fault it raises is
        /// attributable to the payload or to a converter, and both are better logged and dropped
        /// than fatal. The exception is logged, so nothing is lost silently.
        /// </para>
        /// </remarks>
        /// <param name="payload">The raw bytes MQTTnet delivered.</param>
        /// <param name="topicPrefix">The topic the payload arrived on, for the log entry.</param>
        /// <returns>The message, or <c>null</c> when the payload could not be read.</returns>
        public static Message? CreateMessage(byte[] payload, object? topicPrefix)
        {
            try
            {
                var message = JsonSerializer.Deserialize<Message>(
                    payload, JsonSerialisationOptions.Options);

                if (message is null)
                {
                    Log.MqttMessageConsumerDroppedUnreadablePayload(s_logger, topicPrefix);
                }

                return message;
            }
            catch (Exception ex)
            {
                Log.MqttMessageConsumerDroppedMalformedPayload(s_logger, ex, topicPrefix);
                return null;
            }
        }

        private static partial class Log
        {
            [LoggerMessage(Level = LogLevel.Warning, Message = "MQTTMessageConsumer: Dropped a payload on {TopicPrefix} that was the JSON literal null.")]
            public static partial void MqttMessageConsumerDroppedUnreadablePayload(ILogger logger, object? topicPrefix);

            [LoggerMessage(Level = LogLevel.Warning, Message = "MQTTMessageConsumer: Dropped a payload on {TopicPrefix} that could not be read as a message.")]
            public static partial void MqttMessageConsumerDroppedMalformedPayload(ILogger logger, Exception ex, object? topicPrefix);
        }
    }
}
