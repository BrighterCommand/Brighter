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
using System.Diagnostics;
using System.Text.Json.Serialization;
using Paramore.Brighter.Serialization;

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
        /// The date the message was created
        /// </summary>
        public DateTime TimeStamp { get; set; }

        /// <summary>
        /// Gets the identifier.
        /// </summary>
        /// <value>The identifier.</value>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets the topic.
        /// </summary>
        /// <value>The topic.</value>
        public string Topic { get; set; }

        /// <summary>
        /// Gets the type of the message. Used for routing the message to a handler
        /// </summary>
        /// <value>The type of the message.</value>
        public MessageType MessageType { get; set; }

        /// <summary>
        /// A property bag that can be used for extended header attributes.
        /// Use camelCase for the key names if you intend to read it yourself, as when converted to and from Json serializers will tend convert the property
        /// name from UpperCase to camelCase
        /// </summary>
        /// <value>The bag.</value>
        public Dictionary<string, object> Bag { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Gets the number of times this message has been seen 
        /// </summary>
        public int HandledCount { get; set; }

        /// <summary>
        /// Gets the number of milliseconds the message was instructed to be delayed for
        /// </summary>
        public int DelayedMilliseconds { get; set; }

        /// <summary>
        /// Gets or sets the correlation identifier. Used when doing Request-Reply instead of Publish-Subscribe,
        /// allows the originator to match responses to requests
        /// </summary>
        /// <value>The correlation identifier.</value>
        public Guid CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets the ContentType used to describe how the message payload
        /// has been serialized.  Default value is text/plain
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Gets or sets the reply to topic. Used when doing Request-Reply instead of Publish-Subscribe to identify
        /// the queue that the sender is listening on. Usually a sender listens on a private queue, so that they
        /// do not have to filter replies intended for other listeners.
        /// </summary>
        /// <value>The reply to.</value>
        public string ReplyTo { get; set; }

        /// <summary>
        /// If we are working with consistent hashing to distribute writes across multiple channels according to the hash value of a partition key
        /// then we need to be able to set that key, so that we can distribute writes effectively.
        /// </summary>
        public string PartitionKey { get; set; }

        /// <summary>
        /// Gets the telemetry information for the message
        /// </summary>
        public MessageTelemetry Telemetry { get; private set; }

        /// Intended for serialization, prefer a parameterized constructor in application code as a better 'pit of success'
        /// </summary>
        public MessageHeader() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageHeader"/> class.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="topic">The topic.</param>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="correlationId">Used in request-reply to allow the sender to match response to their request</param>
        /// <param name="replyTo">Used for a request-reply message to indicate the private channel to reply to</param>
        /// <param name="contentType">The type of the payload of the message</param>
        /// <param name="partitionKey">How should we group messages that must be processed together i.e. consistent hashing</param>
        public MessageHeader(
            Guid messageId,
            string topic,
            MessageType messageType,
            Guid? correlationId = null,
            string replyTo = "",
            string contentType = "",
            string partitionKey = "")
        {
            Id = messageId;
            Topic = topic;
            MessageType = messageType;
            TimeStamp = RoundToSeconds(DateTime.UtcNow);
            HandledCount = 0;
            DelayedMilliseconds = 0;
            CorrelationId = correlationId ?? Guid.Empty;
            ReplyTo = replyTo;
            ContentType = contentType;
            PartitionKey = partitionKey;
            ReplyTo = replyTo ?? string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageHeader"/> class.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="topic">The topic.</param>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="timeStamp">The time of message creation, will be rounded to seconds</param>
        /// <param name="correlationId">Used in request-reply to allow the sender to match response to their request</param>
        /// <param name="replyTo">Used for a request-reply message to indicate the private channel to reply to</param>
        /// <param name="contentType">The type of the payload of the message, defaults to tex/plain</param>
        /// <param name="partitionKey">How should we group messages that must be processed together i.e. consistent hashing</param>
        public MessageHeader(
            Guid messageId,
            string topic,
            MessageType messageType,
            DateTime timeStamp,
            Guid? correlationId = null,
            string replyTo = null,
            string contentType = "text/plain",
            string partitionKey = "")
            : this(messageId, topic, messageType, correlationId, replyTo, contentType, partitionKey)
        {
            TimeStamp = RoundToSeconds(timeStamp);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageHeader"/> class.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="topic">The topic.</param>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="timeStamp">The time of message creation</param>
        /// <param name="handledCount">The number of attempts to handle this message</param>
        /// <param name="delayedMilliseconds">The delay in milliseconds to this message (usually to retry)</param>
        /// <param name="correlationId">Used in request-reply to allow the sender to match response to their request</param>
        /// <param name="replyTo">Used for a request-reply message to indicate the private channel to reply to</param>
        /// <param name="contentType">The type of the payload of the message, defaults to tex/plain</param>
        /// <param name="partitionKey">How should we group messages that must be processed together i.e. consistent hashing</param>
        public MessageHeader(
            Guid messageId,
            string topic,
            MessageType messageType,
            DateTime timeStamp,
            int handledCount,
            int delayedMilliseconds,
            Guid? correlationId = null,
            string replyTo = null,
            string contentType = "text/plain",
            string partitionKey = "")
            : this(messageId, topic, messageType, timeStamp, correlationId, replyTo, contentType, partitionKey)
        {
            HandledCount = handledCount;
            DelayedMilliseconds = delayedMilliseconds;
        }

        /// <summary>
        /// Create a copy of the header that allows manipulation of bag contents.
        /// </summary>
        /// <returns></returns>
        public MessageHeader Copy()
        {
            var newHeader = new MessageHeader
            {
                Id = Id,
                Topic = $"{Topic}",
                MessageType = MessageType,
                TimeStamp = TimeStamp,
                HandledCount = 0,
                DelayedMilliseconds = 0,
                CorrelationId = CorrelationId,
                ReplyTo = $"{ReplyTo}",
                ContentType = $"{ContentType}",
                PartitionKey = $"{PartitionKey}"
            };

            foreach (var item in Bag)
            {
                newHeader.Bag.Add(item.Key, item.Value);
            }

            return newHeader;
        }


        //AMQP spec says:
        // 4.2.5.4 Timestamps
        // Time stamps are held in the 64-bit POSIX time_t format with an
        // accuracy of one second. By using 64 bits we avoid future wraparound
        // issues associated with 31-bit and 32-bit time_t values.
        private DateTime RoundToSeconds(DateTime dateTime)
        {
            return new DateTime(dateTime.Ticks - (dateTime.Ticks % TimeSpan.TicksPerSecond), dateTime.Kind);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
        public bool Equals(MessageHeader other)
        {
            return Id == other.Id && Topic == other.Topic && MessageType == other.MessageType;
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" /> is equal to this instance.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns><c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.</returns>
        public override bool Equals(object obj)
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
                var hashCode = Id.GetHashCode();
                hashCode = (hashCode * 397) ^ (Topic != null ? Topic.GetHashCode() : 0);
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
        /// Updates the number of times the message has been seen by the dispatcher; used to determine if it is poisioned and should be discarded.
        /// </summary>
        public void UpdateHandledCount()
        {
            HandledCount++;
        }

        /// <summary>
        /// Populate the Telemetry from the HeaderBag
        /// </summary>
        [Obsolete(
            "Looking to remove this in v10 in favour of storing the Cloud Events in their own property in the outbox")]
        public void UpdateTelemetryFromHeaders()
        {
            object eventId;
            object source;
            object eventType;
            object subject;

            if (Bag.TryGetValue(MessageTelemetry.EventIdHeaderName, out eventId) &&
                Bag.TryGetValue(MessageTelemetry.SourceHeaderName, out source) &&
                Bag.TryGetValue(MessageTelemetry.EventTypeHeaderName, out eventType))
            {
                Bag.TryGetValue(MessageTelemetry.SubjectHeaderName, out subject);

                Telemetry = new MessageTelemetry(eventId.ToString(), source.ToString(), eventType.ToString(),
                    subject?.ToString());
            }
        }

        public void AddTelemetryInformation(Activity activity, string eventType)
        {
            Bag[MessageTelemetry.EventIdHeaderName] = activity.Id;
            Bag[MessageTelemetry.SourceHeaderName] = "Brighter"; //ToDo: Plumb in something better than this
            Bag[MessageTelemetry.EventTypeHeaderName] = eventType;

            UpdateTelemetryFromHeaders();
        }
    }
}
