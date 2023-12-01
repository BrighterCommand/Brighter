using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    
    [Collection("CommandProcessor")]
    public class CommandProcessorDepositPostTestsAsync: IDisposable
    {
        
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly Message _message;
        private readonly FakeOutbox _fakeOutbox;
        private readonly FakeMessageProducerWithPublishConfirmation _fakeMessageProducerWithPublishConfirmation;

        public CommandProcessorDepositPostTestsAsync()
        {
            _myCommand.Value = "Hello World";

            _fakeOutbox = new FakeOutbox();
            _fakeMessageProducerWithPublishConfirmation = new FakeMessageProducerWithPublishConfirmation();

            var topic = "MyCommand";
            _message = new Message(
                new MessageHeader(_myCommand.Id, topic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
                );

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
                null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .RetryAsync();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1));

            PolicyRegistry policyRegistry = new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICYASYNC, retryPolicy },
                { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicy }
            };
            
            var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
            {
                { topic, _fakeMessageProducerWithPublishConfirmation },
            });
            
            IAmAnExternalBusService bus = new ExternalBusServices<Message, CommittableTransaction>(producerRegistry, policyRegistry, _fakeOutbox);
        
            CommandProcessor.ClearExtServiceBus();
            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(), 
                policyRegistry,
                messageMapperRegistry,
                bus
            );
        }

        [Fact]
        public async Task When_depositing_a_message_in_the_outbox()
        {
            //act
            var postedMessageId = await _commandProcessor.DepositPostAsync(_myCommand);
            
            //assert
            //message should not be posted
            _fakeMessageProducerWithPublishConfirmation.MessageWasSent.Should().BeFalse();
            
            //message should be in the store
            var depositedPost = _fakeOutbox
                .OutstandingMessages(0)
                .SingleOrDefault(msg => msg.Id == _message.Id);
                
            depositedPost.Should().NotBeNull();
           
            //message should correspond to the command
            depositedPost.Id.Should().Be(_message.Id);
            depositedPost.Body.Value.Should().Be(_message.Body.Value);
            depositedPost.Header.Topic.Should().Be(_message.Header.Topic);
            depositedPost.Header.MessageType.Should().Be(_message.Header.MessageType);
            
            //message should be marked as outstanding if not sent
            var outstandingMessages = await _fakeOutbox.OutstandingMessagesAsync(0);
            var outstandingMessage = outstandingMessages.Single();
            outstandingMessage.Id.Should().Be(_message.Id);
        }
        
        public void Dispose()
        {
            CommandProcessor.ClearExtServiceBus();
        }
     }
}
