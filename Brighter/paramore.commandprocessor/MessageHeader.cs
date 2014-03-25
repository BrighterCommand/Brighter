using System;

namespace paramore.brighter.commandprocessor
{
    public class MessageHeader
    {
        public Guid MessageId { get; private set; }
        public string Topic { get; private set; }

        public MessageHeader(Guid messageId, string topic)
        {
            MessageId = messageId;
            Topic = topic;
        }
    }
}