using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
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
        private readonly Message _message;
        private readonly Message _message2;
        private readonly FakeOutboxSync _fakeOutbox;
        private readonly FakeMessageProducerWithPublishConfirmation _fakeMessageProducerWithPublishConfirmation;

        public CommandProcessorBulkDepositPostTests()
        {
            _myCommand.Value = "Hello World";

            _fakeOutbox = new FakeOutboxSync();
            _fakeMessageProducerWithPublishConfirmation = new FakeMessageProducerWithPublishConfirmation();

            const string topic = "MyCommand";
            _message = new Message(
                new MessageHeader(_myCommand.Id, topic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
                );
            
            _message2 = new Message(
                new MessageHeader(_myCommand2.Id, topic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand2, JsonSerialisationOptions.Options))
            );

            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()));
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

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
                new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>() {{topic, _fakeMessageProducerWithPublishConfirmation},}));
        }


        [Fact]
        public void When_depositing_a_message_in_the_outbox()
        {
            //act
            var postedMessageId = _commandProcessor.DepositPost(new []{_myCommand, _myCommand2});
            
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
            
            //message should be marked as outstanding if not sent
            var outstandingMessages = _fakeOutbox.OutstandingMessages(0);
            outstandingMessages.Count().Should().Be(2);
        }
        
        public void Dispose()
        {
            CommandProcessor.ClearExtServiceBus();
        }
    }
}
