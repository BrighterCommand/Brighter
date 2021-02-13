using System;

namespace Paramore.Brighter.InMemory.Tests.Builders
{
    public class MessageSpecification
    {
        public MessageHeader Header { get; set; }
        public MessageBody Body { get; set; }
    }
    
    public class MessageBuilder
    {
        private readonly MessageSpecification _specification = new MessageSpecification();

        public MessageBuilder()
        {
            _specification.Header = new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT); 
            _specification.Body = new MessageBody("message body");
        }

        public MessageBuilder WithId(Guid id)
        {
            _specification.Header = new MessageHeader(id, _specification.Header.Topic, _specification.Header.MessageType);
            return this;
        }

        public MessageBuilder With(Action<MessageSpecification> action)
        {
            action(_specification);
            return this;
        }

        public static implicit operator Message(MessageBuilder builder)
        {
            return new Message(builder._specification.Header, builder._specification.Body);
        } 
    }
}
