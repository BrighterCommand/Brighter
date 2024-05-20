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

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Deposit
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
        private readonly FakeMessageProducerWithPublishConfirmation _commandProducer;
        private readonly FakeMessageProducerWithPublishConfirmation _eventProducer;

        public CommandProcessorBulkDepositPostTestsAsync()
        {
            const string commandTopic = "MyCommand";
            
            _myCommand.Value = "Hello World";
            _myCommand2.Value = "Update World";

            _commandProducer = new FakeMessageProducerWithPublishConfirmation();
            _commandProducer.Publication = new Publication
            {
                Topic =  new RoutingKey(commandTopic),
                RequestType = typeof(MyCommand)
            };

            const string eventTopic = "MyEvent";
            _eventProducer = new FakeMessageProducerWithPublishConfirmation();
            _eventProducer.Publication = new Publication
            {
                Topic =  new RoutingKey(eventTopic),
                RequestType = typeof(MyEvent)
            };
            
            _message = new Message(
                new MessageHeader(_myCommand.Id, commandTopic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
                );
            
            _message2 = new Message(
                new MessageHeader(_myCommand2.Id, commandTopic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand2, JsonSerialisationOptions.Options))
            );
            
            _message3 = new Message(
                new MessageHeader(_myEvent.Id, eventTopic, MessageType.MT_EVENT),
                new MessageBody(JsonSerializer.Serialize(_myEvent, JsonSerialisationOptions.Options))
            );

            var messageMapperRegistry = new MessageMapperRegistry(
                null,
            new SimpleMessageMapperFactoryAsync((type) =>
            {
                if (type == typeof(MyCommandMessageMapperAsync))
                    return new MyCommandMessageMapperAsync();
                else                              
                    return new MyEventMessageMapperAsync();
            }));
            messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();

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
                    { commandTopic, _commandProducer },
                    { eventTopic, _eventProducer }
                }); 
            
            _fakeOutbox = new FakeOutbox();
            
            IAmAnExternalBusService bus = new ExternalBusService<Message, CommittableTransaction>(
                producerRegistry, 
                policyRegistry,
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                _fakeOutbox
            );
        
            CommandProcessor.ClearServiceBus();
            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(), 
                policyRegistry,
                bus
            );
        }


        [Fact]
        public async Task When_depositing_a_message_in_the_outbox()
        {
            //act
            var requests = new List<IRequest> {_myCommand, _myCommand2, _myEvent } ;
            await _commandProcessor.DepositPostAsync(requests);
            
            //assert
            //message should not be posted
            _commandProducer.MessageWasSent.Should().BeFalse();
            _eventProducer.MessageWasSent.Should().BeFalse();
            
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
            CommandProcessor.ClearServiceBus();
        }
     }
}
