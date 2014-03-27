using System;

namespace paramore.brighter.commandprocessor
{
    public class Message : IEquatable<Message>
    {
        public MessageHeader Header { get; private set; }
        public MessageBody Body { get; private set; }

        public Guid Id 
        {
            get { return Header.Id; }  
        }

        public Message(MessageHeader header, MessageBody body)
        {
            Header = header;
            Body = body;
        }

        public bool Equals(Message other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Header.Id.Equals(other.Header.Id);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Message) obj);
        }

        public override int GetHashCode()
        {
            return Header.Id.GetHashCode();
        }

        public static bool operator ==(Message left, Message right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Message left, Message right)
        {
            return !Equals(left, right);
        }
    }
}