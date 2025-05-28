#region Licence
/* The MIT License (MIT)
Copyright © 2017 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.MessagingGateway.Redis
{
    internal static class HeaderNames
    {
        /// <summary>
        /// The bag for user defined contents
        /// </summary>
        public const string BAG = "Bag";
        /// <summary>
        /// WHat is in the message body?
        /// </summary>
        public const string CONTENT_TYPE = "ContentType";

        /// <summary>
        /// The message type
        /// </summary>
        public const string MESSAGE_TYPE = "MessageType";
        /// <summary>
        /// The message identifier
        /// </summary>
        public const string MESSAGE_ID = "Id";
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
        public const string DELAYED_MILLISECONDS = "DelayedMilliseconds";
        /// <summary>
        /// RPC, who should we reply to 
        /// </summary>
        public const string REPLY_TO = "ReplyTo";
        /// <summary>
        /// The timestamp of the message
        /// </summary>
        public const string TIMESTAMP = "TimeStamp";
        
        /// <summary>
        /// The cloud event dataschema
        /// </summary>
        public const string CLOUD_EVENTS_DATA_SCHEMA = "dataschema";

        /// <summary>
        /// The cloud event subject
        /// </summary>
        public const string CLOUD_EVENTS_SOURCE = "source";
        
        /// <summary>
        /// The cloud event subject
        /// </summary>
        public const string CLOUD_EVENTS_SUBJECT = "subject";
        
        /// <summary>
        /// The cloud event type
        /// </summary>
        public const string CLOUD_EVENTS_TYPE = "type";
        
        /// <summary>
        /// The cloud events traceparent, follows the W3C standard
        /// </summary>
        public const string CLOUD_EVENTS_TRACE_PARENT = "traceparent";

        /// <summary>
        /// The cloud events tracestate, follows the W3C standard
        /// </summary>
        public const string CLOUD_EVENTS_TRACE_STATE = "tracestate";

        /// <summary>
        /// The W3C baggage
        /// </summary>
        public const string W3C_BAGGAGE = "baggage";
    }
}
