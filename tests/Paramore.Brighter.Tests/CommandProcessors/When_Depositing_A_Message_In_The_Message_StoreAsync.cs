using System;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Tests.CommandProcessors
{
    public class CommandProcessorDepositPostTestsAsync
    {
        
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly Message _message;
        private readonly FakeOutbox _fakeOutbox;
        private readonly FakeMessageProducer _fakeMessageProducer;

        public CommandProcessorDepositPostTestsAsync()
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
                .RetryAsync();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1));

            PolicyRegistry policyRegistry = new PolicyRegistry { { CommandProcessor.RETRYPOLICYASYNC, retryPolicy }, { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicy } };
            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                policyRegistry,
                messageMapperRegistry,
                (IAmAnOutboxAsync<Message>)_fakeOutbox,
                (IAmAMessageProducerAsync)_fakeMessageProducer);
        }


        [Fact]
        public async Task When_depositing_a_message_in_the_outbox()
        {
            //act
            var postedMessageId = await _commandProcessor.DepositPostAsync(_myCommand);
            
            //assert
            //message should be in the store
            _fakeOutbox.MessageWasAdded.Should().BeTrue();
            //message should not be posted
            _fakeMessageProducer.MessageWasSent.Should().BeFalse();
            
            //message should correspond to the command
            var depositedPost = _fakeOutbox.Get(postedMessageId);
            depositedPost.Id.Should().Be(_message.Id);
            depositedPost.Body.Value.Should().Be(_message.Body.Value);
            depositedPost.Header.Topic.Should().Be(_message.Header.Topic);
            depositedPost.Header.MessageType.Should().Be(_message.Header.MessageType);
        }
        
        [Fact]
        public void Dispose()
        {
            _commandProcessor.Dispose();
        }
     }
}
