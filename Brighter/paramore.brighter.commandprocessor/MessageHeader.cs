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
    public enum MessageType
    {
        MT_NONE = 0,
        MT_COMMAND = 1,
        MT_EVENT = 2,
        MT_DOCUMENT = 3,
        MT_QUIT = 4
    }

    public class MessageHeader : IEquatable<MessageHeader>
    {
        public Guid Id { get; private set; }
        public string Topic { get; private set; }
        public MessageType MessageType { get; private set; }
        public Dictionary<string, object> Bag { get; private set; } //intended for extended headers

        public MessageHeader(Guid messageId, string topic, MessageType messageType)
        {
            Id = messageId;
            Topic = topic;
            MessageType = messageType;
            Bag = new Dictionary<string, object>();
        }

        public bool Equals(MessageHeader other)
        {
            return Topic == other.Topic && MessageType == other.MessageType;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MessageHeader) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Id.GetHashCode();
                hashCode = (hashCode*397) ^ (Topic != null ? Topic.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (int) MessageType;
                return hashCode;
            }
        }

        public static bool operator ==(MessageHeader left, MessageHeader right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(MessageHeader left, MessageHeader right)
        {
            return !Equals(left, right);
        }
    }
}