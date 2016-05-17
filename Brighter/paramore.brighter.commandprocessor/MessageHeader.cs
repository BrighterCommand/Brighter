// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-01-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

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

namespace paramore.brighter.commandprocessor
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
        MT_QUIT = 4,
    }

    /// <summary>
    /// Class MessageHeader
    /// The header for a <see cref="Message"/>
    /// </summary>
    public class MessageHeader : IEquatable<MessageHeader>
    {
        public DateTime TimeStamp { get; private set; }
        /// <summary>
        /// Gets the identifier.
        /// </summary>
        /// <value>The identifier.</value>
        public Guid Id { get; private set; }
        /// <summary>
        /// Gets the topic.
        /// </summary>
        /// <value>The topic.</value>
        public string Topic { get; set; }
        /// <summary>
        /// Gets the type of the message. Used for routing the message to a handler
        /// </summary>
        /// <value>The type of the message.</value>
        public MessageType MessageType { get; private set; }
        /// <summary>
        /// Gets the bag.
        /// </summary>
        /// <value>The bag.</value>
        public Dictionary<string, object> Bag { get; private set; }
        /// <summary>
        /// Gets the number of times this message has been seen 
        /// </summary>
        public int HandledCount { get; private set; }
        /// <summary>
        /// Gets the number of milliseconds the message was instructed to be delayed for
        /// </summary>
        public int DelayedMilliseconds { get; private set; }

        /// <summary>
        /// Gets or sets the correlation identifier. Used when doing Request-Reply instead of Publish-Subscribe,
        /// allows the originator to match responses to requests
        /// </summary>
        /// <value>The correlation identifier.</value>
        public Guid CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets the ContentType used to describe how the message payload
        /// has been serialized.  Default value is text/plain to remain compatible
        /// with Brigher code that pre-dates adding binary message encoding support
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Gets or sets the reply to topic. Used when doing Request-Reply instead of Publish-Subscribe to identify
        /// the queue that the sender is listening on. Usually a sender listens on a private queue, so that they
        /// do not have to filter replies intended for other listeners.
        /// </summary>
        /// <value>The reply to.</value>
        public string ReplyTo { get; set; }

        public MessageHeader()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageHeader"/> class.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="topic">The topic.</param>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="correlationId">Used in request-reply to allow the sender to match response to their request</param>
        /// <param name="replyTo">Used for a request-reply message to indicate the private channel to reply to</param>
        public MessageHeader(Guid messageId, string topic, MessageType messageType, Guid? correlationId = null, string replyTo = null)
        {
            Id = messageId;
            Topic = topic;
            MessageType = messageType;
            Bag = new Dictionary<string, object>();
            TimeStamp = RoundToSeconds(Clock.Now().Value);
            HandledCount = 0;
            DelayedMilliseconds = 0;
            CorrelationId = correlationId.HasValue ? correlationId.Value : Guid.Empty ;
            ReplyTo = replyTo;

            // Defaulting ContentType to text/plain to match the behavior of Brigher before
            // the addition of binary message body support.
            ContentType = "text/plain";
        }

        public MessageHeader(Guid messageId, string topic, MessageType messageType, DateTime timeStamp, Guid? correlationId = null, string replyTo = null)
            : this(messageId, topic, messageType, correlationId, replyTo)
        {
            TimeStamp = RoundToSeconds(timeStamp);
        }

        public MessageHeader(Guid messageId, string topic, MessageType messageType, DateTime timeStamp, int handledCount, int delayedMilliseconds, Guid? correlationId = null, string replyTo = null)
            : this(messageId, topic, messageType, timeStamp, correlationId, replyTo)
        {
            HandledCount = handledCount;
            DelayedMilliseconds = delayedMilliseconds;
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
                hashCode = (hashCode * 397) ^ TimeStamp.GetHashCode();
                hashCode = (hashCode * 397) ^ HandledCount.GetHashCode();
                hashCode = (hashCode * 397) ^ DelayedMilliseconds.GetHashCode();
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
    }
}