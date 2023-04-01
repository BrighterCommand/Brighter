using System;

namespace Paramore.Brighter.InMemory.Tests.Builders
{
    public class MessageSpecification
    {
        public MessageHeader Header { get; set; }
        public MessageBody Body { get; set; }
    }

    public class MessageTestDataBuilder
    {
        private readonly MessageSpecification _specification = new MessageSpecification();

        public MessageTestDataBuilder()
        {
            _specification.Header = new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT); 
            _specification.Body = new MessageBody("message body");
        }

        public MessageTestDataBuilder WithId(Guid id)
        {
            _specification.Header = new MessageHeader(id, _specification.Header.Topic, _specification.Header.MessageType);
            return this;
        }

        public MessageTestDataBuilder With(Action<MessageSpecification> action)
        {
            action(_specification);
            return this;
        }

        public static implicit operator Message(MessageTestDataBuilder testDataBuilder)
        {
            return new Message(testDataBuilder._specification.Header, testDataBuilder._specification.Body);
        }
    }
}
