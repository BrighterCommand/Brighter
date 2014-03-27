using System;

namespace paramore.brighter.commandprocessor
{
    public class MessageHeader
    {
        public Guid Id { get; private set; }
        public string Topic { get; private set; }

        public MessageHeader(Guid messageId, string topic)
        {
            Id = messageId;
            Topic = topic;
        }
    }
}