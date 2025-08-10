#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text.Json.Serialization;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.NJsonConverters;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter
{
    /// <summary>
    /// Enum MessageType
    /// The type of a message, used on the receiving side of a Task Queue to handle the message appropriately
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// The message could not be read
        /// </summary>
        MT_UNACCEPTABLE = -1,

        /// <summary>
        /// The message type has not been configured
        /// </summary>
        MT_NONE = 0,

        /// <summary>
        /// The message was sent as a command, and the producer intended it to be handled by a consumer
        /// </summary>
        MT_COMMAND = 1,

        /// <summary>
        /// The message was raised as an event and the producer does not care if anyone listens to it
        /// It only contains a simple notification, not the data of what changed
        /// </summary>
        MT_EVENT = 2,

        /// <summary>
        /// The message was raised as an event and the producer does not care if anyone listens to it
        /// It contains a notification of what changed
        /// </summary>
        MT_DOCUMENT = 3,

        /// <summary>
        /// A quit message, used to end a dispatcher's message pump
        /// </summary>
        MT_QUIT = 4
    }

    /// <summary>
    /// Class MessageHeader
    /// The header for a <see cref="Message"/>
    /// </summary>
    public class MessageHeader : IEquatable<MessageHeader>
    {
        /// <summary>
        /// The default Brighter SpecVersion
        /// </summary>
        public const string DefaultSpecVersion = "1.0";

        /// <summary>
        /// The default Brighter source
        /// </summary>
        public const string DefaultSource = "http://goparamore.io";
            
        /// <summary>
        /// A property bag that can be used for extended header attributes.
        /// Use camelCase for the key names if you intend to read it yourself, as when converted to and from Json serializers will tend convert the property
        /// name from UpperCase to camelCase
        /// </summary>
        /// <value>The bag.</value>
        public Dictionary<string, object> Bag { get; set; } = new();
        
        /// <summary>
        /// The Baggage header is a W3C standard and is used to convey user-defined request or workflow information across systems.
        /// It is not intended for telemetry data, but rather for user-defined metadata about a trace.
        /// Each entry consists of a key-value pair where the key identifies the vendor and the value contains user-defined request or workflow data.
        /// Whereas the <see cref="Bag"/> property is used for extended header attributes, the Baggage property is specifically for W3C Baggage.
        /// </summary>
        /// <value>The baggage.</value>
        public Baggage Baggage { get; set; } = new();
        
        /// <summary>
        /// OPTIONAL [Cloud Events] REQUIRED [Brighter]
        /// Content type of data value. This attribute enables data to carry any type of content, whereby format and
        /// encoding might differ from that of the chosen event format.
        /// MUST adhere to the format specified in <see href="https://datatracker.ietf.org/doc/html/rfc2046">RFC 2046</see>
        /// Because of the complexity of serializing if you do not know the type, we regard this as required even
        /// though Cloud Events does not.
        /// Default value is "appliacation/json; charset=utf-8"
        /// </summary>
        /// <value>The content type.</value>
        public ContentType ContentType { get; set; }

        /// <summary>
        /// Gets or sets the correlation identifier. Used when doing Request-Reply instead of Publish-Subscribe,
        /// allows the originator to match responses to requests
        /// </summary>
        /// <value>The correlation identifier.</value>
        [JsonConverter(typeof(IdConverter))]
        [Newtonsoft.Json.JsonConverter(typeof(NIdConverter))]
        public Id CorrelationId { get; set; } = new("");

        /// <summary>
        /// OPTIONAL
        /// From <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#context-attributes">Cloud Events Spec</see>
        /// Identifies the schema that data adheres to. Incompatible changes to the schema SHOULD be reflected by a different URI. 
        /// </summary>
        /// <value>The data schema.</value>
        public Uri? DataSchema { get; set; }
        
        /// <summary>
        /// OPTIONAL
        /// From <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/extensions/dataref.md">Cloud Events Spec</see>
        /// A reference to a location where the event payload is stored. The location MAY not be accessible without further information (e.g. a pre-shared secret).
         ///Known as the "Claim Check Pattern", this attribute MAY be used for a variety of purposes:
        ///  - If the Data is too large to be included in the message, the data is not present, and the consumer can retrieve it using this attribute.
        ///  - If the consumer wants to verify that the Data has not been tampered with, it can retrieve it from a trusted source using this attribute.
        ///  - If the Data MUST only be viewed by trusted consumers (e.g. personally identifiable information), only a trusted consumer can retrieve it using this attribute and a pre-shared secret.
        /// </summary>
        /// <value>The data reference.</value>
        public string? DataRef { get; set; }
        
        /// <summary>
        /// OPTIONAL
        /// Internal usage. Gets the period the message was instructed to be delayed for
        /// </summary>
        /// <value>The delay.</value>
        public TimeSpan Delayed { get; set; }
        
        /// <summary>
        /// OPTIONAL
        /// Internal usage. Gets the number of times this message has been seen 
        /// </summary>
        /// <value>The number of times this message has been handled.</value>
        public int HandledCount { get; set; }
        
        /// <summary>
        /// The identifier for an instance of a workflow. This is used to correlate messages that are part of a workflow.
        /// </summary>
        /// <remarks> Reserved for future use, currently not used in Brighter</remarks>
        /// <value>The job identifier.</value>
        [JsonConverter(typeof(IdConverter))]
        [Newtonsoft.Json.JsonConverter(typeof(NIdConverter))]
        public Id? JobId { get; }

        /// <summary>
        /// REQUIRED
        /// From <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#context-attributes">Cloud Events Spec</see>
        /// Identifies the event. Producers MUST ensure that source + id is unique for each distinct event.
        /// If a duplicate event is re-sent (e.g. due to a network error) it MAY have the same id.
        /// Consumers MAY assume that Events with identical source and id are duplicates.
        /// </summary>
        /// <value>The identifier.</value>
        [JsonConverter(typeof(IdConverter))]
        [Newtonsoft.Json.JsonConverter(typeof(NIdConverter))]
        public Id MessageId { get; init; } = new("");

        /// <summary>
        /// REQUIRED
        /// Gets the type of the message (command, event). Internal usage, Used when routing the message to a handler
        /// </summary>
        /// <value>The type of the message.</value>
        public MessageType MessageType { get; init; }

        /// <summary>
        /// OPTIONAL
        /// From <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/extensions/partitioning.md">Cloud Events Spec</see>       
        /// If we are working with consistent hashing to distribute writes across multiple channels according to the
        /// hash value of a partition key then we need to be able to set that key, so that we can distribute writes effectively.
        /// </summary>
        /// <value>The partition key.</value>
        public PartitionKey PartitionKey { get; set; } = PartitionKey.Empty;
        
        /// <summary>
        /// OPTIONAL
        /// Gets or sets the reply to topic. Used when doing Request-Reply instead of Publish-Subscribe to identify
        /// the queue that the sender is listening on. Usually a sender listens on a private queue, so that they
        /// do not have to filter replies intended for other listeners.
        /// </summary>
        /// <value>The reply to.</value>
        [JsonConverter(typeof(RoutingKeyConvertor))]
        [Newtonsoft.Json.JsonConverter(typeof(RoutingKeyConvertor))]
        public RoutingKey? ReplyTo { get; set; }
        
        /// <summary>
        /// OPTIONAL
        /// From <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#context-attributes">Cloud Events Spec</see>
        /// This describes the subject of the event in the context of the event producer (identified by source).
        /// In publish-subscribe scenarios, a subscriber will typically subscribe to events emitted by a source,
        /// but the source identifier alone might not be sufficient as a qualifier for any specific event if the
        /// source context has internal sub-structure.
        /// </summary>
        public string? Subject { get; set; }

        /// <summary>
        /// REQUIRED
        /// The version of the CloudEvents specification which the event uses. This enables the interpretation of the context.
        /// Defaults tp 1.0
        /// </summary>
        /// <value>The spec version.</value>
        public string SpecVersion { get; set; } = DefaultSpecVersion;
        
        /// <summary>
        /// REQUIRED
        /// From <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#context-attributes">Cloud Events Spec</see>
        /// Identifies the context in which an event happened. Often this will include information such as the type of
        /// the event source, the organization publishing the event or the process that produced the event.
        /// The exact syntax and semantics behind the data encoded in the URI is defined by the event producer.
        /// Producers MUST ensure that source + id is unique for each distinct event.
        /// Default: "http://goparamore.io" for backward compatibility as required
        /// </summary>
        public Uri Source { get; set; } = new Uri(DefaultSource);

        /// <summary>
        /// REQUIRED for sending. OPTIONAL for receiving
        /// Gets the name of the channel that the message is on. Whilst this can be inferred from the message type, it is
        /// used internally when we send the message to its destination
        /// </summary>
        /// <value>The topic.</value>
        [JsonConverter(typeof(RoutingKeyConvertor))]
        [Newtonsoft.Json.JsonConverter(typeof(RoutingKeyConvertor))]
        public RoutingKey Topic { get; init; } = RoutingKey.Empty;

        /// <summary>
        /// REQUIRED
        /// Internal usage. The date the message was created.
        /// Defaults to now in UTC
        /// </summary>
        /// <value>The time stamp.</value>
        public DateTimeOffset TimeStamp { get; init; }
        
        /// <summary>
        /// REQUIRED (for OTel support, OPTIONAL in Cloud Events)
        /// From <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/extensions/distributed-tracing.md">Cloud Events Spec</see>
        /// See also <see href = "https://w3c.github.io/trace-context/#traceparent-header">W3C Trace Context</see>
        ///  The traceparent HTTP header field identifies the incoming request in a tracing system. It has four fields:
        ///     - version
        ///     - trace-id
        ///     - parent-id
        ///     - trace-flags
        /// In .NET it is set from Activity.Current.Id
        /// </summary>
        /// <value>The traceparent.</value>
        [JsonConverter(typeof(TraceParentConverter))]
        [Newtonsoft.Json.JsonConverter(typeof(TraceParentConverter))]
        public TraceParent? TraceParent { get; set; }

        /// <summary>
        /// OPTIONAL
        /// From <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/extensions/distributed-tracing.md">Cloud Events Spec</see>
        /// See also <see href = "https://w3c.github.io/trace-context/#tracestate-header">W3C Trace Context</see>
        /// Provides additional vendor-specific trace identification information across different distributed tracing systems
        /// and is a companion header for the traceparent field. It also conveys information about the request’s position
        /// in multiple distributed tracing graphs.
        /// The tracestate HTTP header MUST NOT be used for any properties that are not defined by a tracing system. 
        /// </summary>
        /// <value>The tracestate.</value>
        [JsonConverter(typeof(TraceStateConverter))]
        [Newtonsoft.Json.JsonConverter(typeof(TraceStateConverter))]
        public TraceState? TraceState { get; set; }

        /// <summary>
        /// REQUIRED
        /// From <see href="https://github.com/cloudevents/spec/blob/v1.0.2/cloudevents/spec.md#context-attributes">Clode Events Spec</see>
        /// This attribute contains a value describing the type of event related to the originating occurrence.
        /// Often this attribute is used for routing, observability, policy enforcement, etc.
        /// SHOULD be prefixed with a reverse-DNS name. The prefixed domain dictates the organization which defines the semantics of this event type.
        /// Default to empty string
        /// </summary>
        /// <value>The type of event as a <see cref="CloudEventsType"/>.</value>
        public CloudEventsType Type { get; set; } = CloudEventsType.Empty;
        
        /// <summary>
        /// The identity of the workflow this message was sent as part of. This is used to correlate messages that are part of a workflow.
        /// </summary>
        /// <value>The workflow identifier.</value>
        /// <remarks> Reserved for future use, currently not used in Brighter</remarks>
        [JsonConverter(typeof(IdConverter))]
        [Newtonsoft.Json.JsonConverter(typeof(NIdConverter))]
        public Id? WorkflowId { get; }

        /// <summary>
        /// Intended for serialization, prefer the parameterized constructor in application code as a better 'pit of success'
        /// </summary>
        public MessageHeader()
        {
            ContentType = new ContentType("application/json; charset=utf-8");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageHeader"/> class.
        /// </summary>
        /// <param name="messageId">Identifies the event. Producers MUST ensure that source + id is unique for each distinct event.</param>
        /// <param name="topic">The name of the channel that the message is on</param>
        /// <param name="messageType">Type of message: command; event.</param>
        /// <param name="source">Identifies the context in which an event happened; often a URI identifying the producer</param>
        /// <param name="type">The type of event; SHOULD be prefixed with a reverse-DNS name </param>
        /// <param name="timeStamp">The time of message creation, will be rounded to seconds</param>
        /// <param name="correlationId">Used in request-reply to allow the sender to match response to their request</param>
        /// <param name="replyTo">Used for a request-reply message to indicate the private channel to reply to</param>
        /// <param name="contentType">The type of the payload of the message, defaults to application/json and utf8</param>
        /// <param name="partitionKey">How should we group messages that must be processed together i.e. consistent hashing</param>
        /// <param name="dataSchema">A Uri that identifies the schema that data adheres to</param>
        /// <param name="subject">Describes the subject of the event in the context of the event producer</param>
        /// <param name="workflowId">The identity of the workflow this message was sent as part of</param>
        /// <param name="jobId">The identifier for an instance of that workflow</param>
        /// <param name="handledCount">The number of attempts to handle this message</param>
        /// <param name="delayed">The delay in milliseconds to this message (usually to retry)</param>
        /// <param name="traceParent">The traceparent for this message; follows the W3C standard</param>
        /// <param name="traceState">The tracestate for this message; follows the W3C standard</param>
        /// <param name="baggage">The baggage for this message; follows the W3C standard</param>
        public MessageHeader(
            Id messageId,
            RoutingKey topic,
            MessageType messageType,
            Uri? source = null,
            CloudEventsType? type = null,
            DateTimeOffset? timeStamp = null,
            Id? correlationId = null,
            RoutingKey? replyTo = null,
            ContentType? contentType = null,
            PartitionKey? partitionKey = null,
            Uri? dataSchema = null,
            string? subject = null,
            int handledCount = 0,
            Id? workflowId = null,
            Id? jobId = null,
            TimeSpan? delayed = null,
            TraceParent? traceParent = null,
            TraceState? traceState = null,
            Baggage? baggage = null)
        {
            MessageId = messageId;
            Topic = topic;
            MessageType = messageType;
            if (source != null) Source = source;
            Type = type ?? CloudEventsType.Empty;
            TimeStamp = timeStamp ?? DateTimeOffset.UtcNow;
            HandledCount = handledCount;
            WorkflowId = workflowId;
            JobId = jobId;
            Delayed = delayed ?? TimeSpan.Zero;
            CorrelationId = correlationId ?? new Id(string.Empty);
            ReplyTo = replyTo ?? RoutingKey.Empty;
            ContentType = contentType ?? new ContentType("application/json; charset=utf-8");
            PartitionKey = partitionKey ?? PartitionKey.Empty;
            DataSchema = dataSchema;
            Subject = subject;
            TraceParent = traceParent;
            TraceState = traceState;
            Baggage = baggage ?? new Baggage();
        }

        /// <summary>
        /// Create a copy of the header that allows manipulation of bag contents.
        /// </summary>
        /// <returns></returns>
        public MessageHeader Copy()
        {
            var newHeader = new MessageHeader(MessageId,
                new RoutingKey($"{Topic}"),
                MessageType,
                timeStamp : TimeStamp,
                handledCount : 0,
                delayed : TimeSpan.Zero,
                correlationId: CorrelationId,
                replyTo : new RoutingKey($"{ReplyTo}"),
                contentType : ContentType,
                partitionKey : PartitionKey
            );

            foreach (var item in Bag)
            {
                newHeader.Bag.Add(item.Key, item.Value);
            }

            return newHeader;
        }
        
        /// <summary>
        /// We return an MT_UNACCEPTABLE message because we cannot process. Really this should go on to an
        /// Invalid Message Queue provided by the Control Bus
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="messageId"></param>
        /// <returns></returns>
        public static MessageHeader FailureMessageHeader(RoutingKey? topic, Id? messageId)
        {
            return new MessageHeader(
                messageId ?? Id.Empty,
                topic ?? RoutingKey.Empty,
                MessageType.MT_UNACCEPTABLE);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
        public bool Equals(MessageHeader? other)
        {
            if (ReferenceEquals(null, other)) return false;

            //We choose to break these into individual comparisons to make it easier to debug
            bool messageIdEquals = MessageId == other.MessageId;
            bool topicEquals = Topic == other.Topic;
            bool messageTypeEquals = MessageType == other.MessageType;
            bool sourceEquals = Source == other.Source;
            bool typeEquals = Type == other.Type;
            bool correlationIdEquals = CorrelationId == other.CorrelationId;
            bool replyToEquals = ReplyTo == other.ReplyTo;
            bool contentTypeEquals = ContentType.Equals(other.ContentType);
            bool partitionKeyEquals = PartitionKey.Equals(other.PartitionKey);
            bool dataSchemaEquals = DataSchema == other.DataSchema;
            bool subjectEquals = Subject == other.Subject;
            bool specVersionEquals = SpecVersion == other.SpecVersion;
            bool workflowIdEquals = WorkflowId == other.WorkflowId;
            bool jobIdEquals = JobId == other.JobId;
            bool handledCountEquals = HandledCount == other.HandledCount;
            bool delayedEquals = Delayed.Equals(other.Delayed);

            return messageIdEquals && topicEquals && messageTypeEquals && sourceEquals &&
                   typeEquals && correlationIdEquals && replyToEquals && contentTypeEquals &&
                   partitionKeyEquals && dataSchemaEquals && subjectEquals && specVersionEquals &&
                   workflowIdEquals && jobIdEquals && handledCountEquals && delayedEquals;
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns><c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.</returns>
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MessageHeader)obj);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = MessageId.GetHashCode();
                hashCode = (hashCode * 397) ^ Topic.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)MessageType;
                return hashCode;
            }
        }

        /// <summary>
        /// Implements the ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator ==(MessageHeader left, MessageHeader right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Implements the !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>The result of the operator.</returns>
        public static bool operator !=(MessageHeader left, MessageHeader right)
        {
            return !Equals(left, right);
        }

        /// <summary>
        /// Updates the number of times the message has been seen by the dispatcher; used to determine if it is poisoned and should be discarded.
        /// </summary>
        public void UpdateHandledCount()
        {
            HandledCount++;
        }
    }
}
