#region Licence

/* The MIT License (MIT)
Copyright © 2020 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    public class HeaderNames
    {
        /// <summary>
        /// A dictionary of user defined values
        /// </summary>
        public const string BAG = "Bag";

        /// <summary>
        /// What is the content type of the message
        /// </summary>
        public const string CONTENT_TYPE  = "ContentType";
        
        /// <summary>
        /// The correlation id
        /// </summary>
        public const string CORRELATION_ID = "CorrelationId";

        /// <summary>
        /// If the message was deferred, how long for?
        /// </summary>
        public const string DELAYED_MILLISECONDS = "x-delay";

        /// <summary>
        /// How many times has the message been retried with a delay
        /// </summary>
        public const string HANDLED_COUNT = "HandledCount";

        /// <summary>
        /// The message type
        /// </summary>
        public const string MESSAGE_TYPE = "MessageType";

        /// <summary>
        /// The message identifier
        /// </summary>
        public const string MESSAGE_ID = "MessageId";

       /// <summary>
       /// The topic we were sent to, before being diverted to DLQ or invalid message channel
       /// </summary>
       public const string ORIGINAL_TOPIC = "OriginalTopic";
       
       /// <summary>
       /// The original type of the message (as may have changed to MT_UNACCEPTABLE)
       /// </summary>
       public const string ORIGINAL_TYPE = "OriginalType";

        /// <summary>
        /// The key used to partition this message
        /// </summary>
        public const string PARTITIONKEY = "PartitionKey";

        /// <summary>
        /// What is the offset into the partition of the message
        /// </summary>
        public const string PARTITION_OFFSET = "TopicPartitionOffset";
        
        ///<summary>
        /// Why was the message rejected?
        /// </summary>
        public const string REJECTION_MESSAGE = "RejectionMessage";
        
        /// <summary>
        /// Why was this message rejected
        /// </summary>
        public const string REJECTION_REASON = "RejectionReason";

        /// <summary>
        /// If we rejected the message, when did we?
        /// </summary>
        public const string REJECTION_TIMESTAMP = "RejectionTimestamp";
        
        /// <summary>
        /// Used for a request-reply message to indicate the private channel to reply to
        /// </summary>
        public const string REPLY_TO = "ReplyTo";
        
        /// <summary>
        /// The time that message was sent
        /// </summary>
        public const string TIMESTAMP = "TimeStamp";

        /// <summary>
        /// The topic
        /// </summary>
        public const string TOPIC = "Topic";

        /// <summary>
        /// The cloud event ID
        /// </summary>
        public const string CLOUD_EVENTS_ID = "ce_id";

        /// <summary>
        /// The cloud event spec version
        /// </summary>
        public const string CLOUD_EVENTS_SPEC_VERSION = "ce_specversion";

        /// <summary>
        /// The cloud event type
        /// </summary>
        public const string CLOUD_EVENTS_TYPE = "ce_type";

        /// <summary>
        /// The cloud event time
        /// </summary>
        public const string CLOUD_EVENTS_TIME = "ce_time";

        /// <summary>
        /// The cloud event subject
        /// </summary>
        public const string CLOUD_EVENTS_SUBJECT = "ce_subject";

        /// <summary>
        /// The cloud event dataschema
        /// </summary>
        public const string CLOUD_EVENTS_DATA_SCHEMA = "ce_dataschema";

        /// <summary>
        /// The cloud event subject
        /// </summary>
        public const string CLOUD_EVENTS_SOURCE = "ce_source";
        
        /// <summary>
        /// The cloud events for content-type
        /// </summary>
        public const string CLOUD_EVENTS_DATA_CONTENT_TYPE  = "content-type";
        
        /// <summary>
        /// The cloud events traceparent, follows the W3C standard
        /// </summary>
        public const string CLOUD_EVENTS_TRACE_PARENT = "ce_traceparent";

        /// <summary>
        /// The cloud events tracestate, follows the W3C standard
        /// </summary>
        public const string CLOUD_EVENTS_TRACE_STATE = "ce_tracestate";

        /// <summary>
        /// The W3C baggage
        /// </summary>
        public const string W3C_BAGGAGE = "baggage";
    }
}
