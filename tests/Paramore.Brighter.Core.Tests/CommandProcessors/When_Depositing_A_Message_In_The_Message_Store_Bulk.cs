using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    [Collection("CommandProcessor")]
    public class CommandProcessorBulkDepositPostTests : IDisposable
    {

        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly MyCommand _myCommand2 = new MyCommand();
        private readonly MyEvent _myEvent = new MyEvent();
        private readonly Message _message;
        private readonly Message _message2;
        private readonly Message _message3;
        private readonly FakeOutboxSync _fakeOutbox;
        private readonly FakeMessageProducerWithPublishConfirmation _fakeMessageProducerWithPublishConfirmation;

        public CommandProcessorBulkDepositPostTests()
        {
            _myCommand.Value = "Hello World";

            _fakeOutbox = new FakeOutboxSync();
            _fakeMessageProducerWithPublishConfirmation = new FakeMessageProducerWithPublishConfirmation();

            const string topic = "MyCommand";
            var eventTopic = "MyEvent";
            _message = new Message(
                new MessageHeader(_myCommand.Id, topic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
                );

            _message2 = new Message(
                new MessageHeader(_myCommand2.Id, topic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand2, JsonSerialisationOptions.Options))
            );

            _message3 = new Message(
                new MessageHeader(_myEvent.Id, eventTopic, MessageType.MT_EVENT),
                new MessageBody(JsonSerializer.Serialize(_myEvent, JsonSerialisationOptions.Options))
            );

            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((type) =>
            {
                if (type.Equals(typeof(MyCommandMessageMapper)))
                    return new MyCommandMessageMapper();
                else
                {
                    return new MyEventMessageMapper();
                }
            }));
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                new PolicyRegistry { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } },
                messageMapperRegistry,
                _fakeOutbox,
                new ProducerRegistry(new Dictionary<string, IAmAMessageProducer> {{topic, _fakeMessageProducerWithPublishConfirmation},}));
        }


        [Fact]
        public void When_depositing_a_message_in_the_outbox()
        {
            //act
            var requests = new List<IRequest> {_myCommand, _myCommand2, _myEvent } ;
            var postedMessageId = _commandProcessor.DepositPost(requests);

            //assert

            //message should not be posted
            _fakeMessageProducerWithPublishConfirmation.MessageWasSent.Should().BeFalse();

            //message should correspond to the command
            var depositedPost = _fakeOutbox.Get(_message.Id);
            depositedPost.Id.Should().Be(_message.Id);
            depositedPost.Body.Value.Should().Be(_message.Body.Value);
            depositedPost.Header.Topic.Should().Be(_message.Header.Topic);
            depositedPost.Header.MessageType.Should().Be(_message.Header.MessageType);

            var depositedPost2 = _fakeOutbox.Get(_message2.Id);
            depositedPost2.Id.Should().Be(_message2.Id);
            depositedPost2.Body.Value.Should().Be(_message2.Body.Value);
            depositedPost2.Header.Topic.Should().Be(_message2.Header.Topic);
            depositedPost2.Header.MessageType.Should().Be(_message2.Header.MessageType);


            var depositedPost3 = _fakeOutbox
                .OutstandingMessages(0)
                .SingleOrDefault(msg => msg.Id == _message3.Id);
            //message should correspond to the command
            depositedPost3.Id.Should().Be(_message3.Id);
            depositedPost3.Body.Value.Should().Be(_message3.Body.Value);
            depositedPost3.Header.Topic.Should().Be(_message3.Header.Topic);
            depositedPost3.Header.MessageType.Should().Be(_message3.Header.MessageType);

            //message should be marked as outstanding if not sent
            var outstandingMessages = _fakeOutbox.OutstandingMessages(0);
            outstandingMessages.Count().Should().Be(3);
        }

        public void Dispose()
        {
            CommandProcessor.ClearExtServiceBus();
        }
    }
}
