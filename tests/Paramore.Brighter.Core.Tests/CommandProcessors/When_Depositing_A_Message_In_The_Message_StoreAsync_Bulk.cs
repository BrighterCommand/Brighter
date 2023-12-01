using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{

    [Collection("CommandProcessor")]
    public class CommandProcessorBulkDepositPostTestsAsync: IDisposable
    {
        
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly MyCommand _myCommand2 = new MyCommand();
        private readonly MyEvent _myEvent = new MyEvent();
        private readonly Message _message;
        private readonly Message _message2;
        private readonly Message _message3;
        private readonly FakeOutbox _fakeOutbox;
        private readonly FakeMessageProducerWithPublishConfirmation _fakeMessageProducerWithPublishConfirmation;

        public CommandProcessorBulkDepositPostTestsAsync()
        {
            _myCommand.Value = "Hello World";
            _myCommand2.Value = "Update World";

            _fakeOutbox = new FakeOutbox();
            _fakeMessageProducerWithPublishConfirmation = new FakeMessageProducerWithPublishConfirmation();

            var topic = "MyCommand";
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
            }), null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

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

            var producerRegistry =
                new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
                {
                    { topic, _fakeMessageProducerWithPublishConfirmation },
                }); 
            
            IAmAnExternalBusService bus = new ExternalBusServices<Message, CommittableTransaction>(producerRegistry, policyRegistry, _fakeOutbox);
        
            CommandProcessor.ClearExtServiceBus();
            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(), 
                policyRegistry,
                bus,
                messageMapperRegistry);
        }


        [Fact]
        public async Task When_depositing_a_message_in_the_outbox()
        {
            //act
            var requests = new List<IRequest> {_myCommand, _myCommand2, _myEvent } ;
            await _commandProcessor.DepositPostAsync(requests);
            
            //assert
            //message should not be posted
            _fakeMessageProducerWithPublishConfirmation.MessageWasSent.Should().BeFalse();
            
            //message should be in the store
            var depositedPost = _fakeOutbox
                .OutstandingMessages(0)
                .SingleOrDefault(msg => msg.Id == _message.Id);
            
            //message should be in the store
            var depositedPost2 = _fakeOutbox
                .OutstandingMessages(0)
                .SingleOrDefault(msg => msg.Id == _message2.Id);
            
            //message should be in the store
            var depositedPost3 = _fakeOutbox
                .OutstandingMessages(0)
                .SingleOrDefault(msg => msg.Id == _message3.Id);

            depositedPost.Should().NotBeNull();
           
            //message should correspond to the command
            depositedPost.Id.Should().Be(_message.Id);
            depositedPost.Body.Value.Should().Be(_message.Body.Value);
            depositedPost.Header.Topic.Should().Be(_message.Header.Topic);
            depositedPost.Header.MessageType.Should().Be(_message.Header.MessageType);
            
            //message should correspond to the command
            depositedPost2.Id.Should().Be(_message2.Id);
            depositedPost2.Body.Value.Should().Be(_message2.Body.Value);
            depositedPost2.Header.Topic.Should().Be(_message2.Header.Topic);
            depositedPost2.Header.MessageType.Should().Be(_message2.Header.MessageType);
            
            //message should correspond to the command
            depositedPost3.Id.Should().Be(_message3.Id);
            depositedPost3.Body.Value.Should().Be(_message3.Body.Value);
            depositedPost3.Header.Topic.Should().Be(_message3.Header.Topic);
            depositedPost3.Header.MessageType.Should().Be(_message3.Header.MessageType);
        }
        
        public void Dispose()
        {
            CommandProcessor.ClearExtServiceBus();
        }
     }
}
