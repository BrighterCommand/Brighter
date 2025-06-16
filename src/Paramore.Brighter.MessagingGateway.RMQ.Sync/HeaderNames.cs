#region Licence

/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

namespace Paramore.Brighter.MessagingGateway.RMQ.Sync
{
    public class HeaderNames
    {
        /// <summary>
        /// The message type
        /// </summary>
        public const string MESSAGE_TYPE = "MessageType";

        /// <summary>
        /// The message identifier
        /// </summary>
        public const string MESSAGE_ID = "MessageId";

        /// <summary>
        /// The correlation id
        /// </summary>
        public const string CORRELATION_ID = "CorrelationId";

        /// <summary>
        /// The topic
        /// </summary>
        public const string TOPIC = "Topic";

        /// <summary>
        /// The handled count
        /// </summary>
        public const string HANDLED_COUNT = "HandledCount";

        /// <summary>
        /// The milliseconds to delay the message by (requires plugin rabbitmq_delayed_message_exchange)
        /// </summary>
        public const string DELAY_MILLISECONDS = "x-delay";

        /// <summary>
        /// The milliseconds the message was instructed to be delayed for (sent as negative) (requires plugin rabbitmq_delayed_message_exchange)
        /// </summary>
        public const string DELAYED_MILLISECONDS = "x-delay";

        /// <summary>
        /// Indicates the original id of this message given a historic scenario (e.g. re-queueing).
        /// </summary>
        public const string ORIGINAL_MESSAGE_ID = Message.OriginalMessageIdHeaderName;

        /// <summary>
        /// Tag used to identify this message in the sequence against its Id (used to perform multiple ack against Id upto Tag).
        /// </summary>
        public const string DELIVERY_TAG = Message.DeliveryTagHeaderName;

        /// <summary>
        /// The cloud event ID
        /// </summary>
        public const string CLOUD_EVENTS_ID = "cloudEvents:id";

        /// <summary>
        /// The cloud event spec version
        /// </summary>
        public const string CLOUD_EVENTS_SPEC_VERSION = "cloudEvents:specversion";

        /// <summary>
        /// The cloud event type
        /// </summary>
        public const string CLOUD_EVENTS_TYPE = "cloudEvents:type";

        /// <summary>
        /// The cloud event time
        /// </summary>
        public const string CLOUD_EVENTS_TIME = "cloudEvents:time";

        /// <summary>
        /// The cloud event subject
        /// </summary>
        public const string CLOUD_EVENTS_SUBJECT = "cloudEvents:subject";

        /// <summary>
        /// The cloud event dataschema
        /// </summary>
        public const string CLOUD_EVENTS_DATA_SCHEMA = "cloudEvents:dataschema";

        /// <summary>
        /// The cloud event subject
        /// </summary>
        public const string CLOUD_EVENTS_SOURCE = "cloudEvents:source";

        /// <summary>
        /// The cloud events traceparent, follows the W3C standard
        /// </summary>
        public const string CLOUD_EVENTS_TRACE_PARENT = "cloudEvents:traceparent";

        /// <summary>
        /// The cloud events tracestate, follows the W3C standard
        /// </summary>
        public const string CLOUD_EVENTS_TRACE_STATE = "cloudevents:tracestate";

        /// <summary>
        /// The W3C baggage
        /// </summary>
        public const string W3C_BAGGAGE = "baggage";
    }
}
