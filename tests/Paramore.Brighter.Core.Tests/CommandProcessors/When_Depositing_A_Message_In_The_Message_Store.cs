using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    public class CommandProcessorDepositPostTests
    {
        
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly Message _message;
        private readonly FakeOutbox _fakeOutbox;
        private readonly FakeMessageProducer _fakeMessageProducer;

        public CommandProcessorDepositPostTests()
        {
            _myCommand.Value = "Hello World";

            _fakeOutbox = new FakeOutbox();
            _fakeMessageProducer = new FakeMessageProducer();

            _message = new Message(
                new MessageHeader(_myCommand.Id, "MyCommand", MessageType.MT_COMMAND),
                new MessageBody(JsonConvert.SerializeObject(_myCommand))
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
                (IAmAnOutbox<Message>)_fakeOutbox,
                (IAmAMessageProducer)_fakeMessageProducer);
        }


        [Fact]
        public void When_depositing_a_message_in_the_outbox()
        {
            //act
            var postedMessageId = _commandProcessor.DepositPost(_myCommand);
            
            //assert
            
            //message should not be posted
            _fakeMessageProducer.MessageWasSent.Should().BeFalse();
            
            //message should correspond to the command
            var depositedPost = _fakeOutbox.Get(postedMessageId);
            depositedPost.Id.Should().Be(_message.Id);
            depositedPost.Body.Value.Should().Be(_message.Body.Value);
            depositedPost.Header.Topic.Should().Be(_message.Header.Topic);
            depositedPost.Header.MessageType.Should().Be(_message.Header.MessageType);
            
            //message should be marked as outstanding if not sent
            var outstandingMessages = _fakeOutbox.OutstandingMessages(1000);
            var outstandingMessage = outstandingMessages.Single();
            outstandingMessage.Id.Should().Be(_message.Id);
        }
        
        [Fact]
        public void Dispose()
        {
            _commandProcessor.Dispose();
        }
    }
}
