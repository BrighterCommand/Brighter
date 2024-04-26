using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
    public class CommandProcessorBulkDepositPostTests : IDisposable
    {
        
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new();
        private readonly MyCommand _myCommandTwo = new();
        private readonly MyEvent _myEvent = new();
        private readonly Message _message;
        private readonly Message _messageTwo;
        private readonly Message _messageThree;
        private readonly FakeOutbox _fakeOutbox;
        private readonly FakeMessageProducerWithPublishConfirmation _commandProducer;
        private readonly FakeMessageProducerWithPublishConfirmation _eventProducer;

        public CommandProcessorBulkDepositPostTests()
        {
            const string topic = "MyCommand";
            _myCommand.Value = "Hello World";
            
            _commandProducer = new FakeMessageProducerWithPublishConfirmation();
            _commandProducer.Publication = new Publication 
            { 
                Topic = new RoutingKey(topic), 
                RequestType = typeof(MyCommand) 
            };
            
            const string eventTopic = "MyEvent";
            _eventProducer = new FakeMessageProducerWithPublishConfirmation();
            _eventProducer.Publication = new Publication 
            { 
                Topic = new RoutingKey(eventTopic), 
                RequestType = typeof(MyEvent) 
            };
            
            _message = new Message(
                new MessageHeader(_myCommand.Id, topic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
                );
            
            _messageTwo = new Message(
                new MessageHeader(_myCommandTwo.Id, topic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommandTwo, JsonSerialisationOptions.Options))
            );
            
            _messageThree = new Message(
                new MessageHeader(_myEvent.Id, eventTopic, MessageType.MT_EVENT),
                new MessageBody(JsonSerializer.Serialize(_myEvent, JsonSerialisationOptions.Options))
            );

            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((type) =>
            {
                if (type == typeof(MyCommandMessageMapper))
                    return new MyCommandMessageMapper();
                else
                    return new MyEventMessageMapper();
            }), null);
            
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));
            
            var producerRegistry = new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
            {
                { topic, _commandProducer },
                { eventTopic, _eventProducer}
            });

            var policyRegistry = new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICY, retryPolicy },
                { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy }
            };
           
            _fakeOutbox = new FakeOutbox();
            
            IAmAnExternalBusService bus = new ExternalBusService<Message, CommittableTransaction>(
                producerRegistry, 
                policyRegistry,
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                new InMemoryRequestContextFactory(),
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
        public void When_depositing_a_message_in_the_outbox()
        {
            //act
            var requests = new List<IRequest> {_myCommand, _myCommandTwo, _myEvent } ;
            var postedMessageId = _commandProcessor.DepositPost(requests);
            
            //assert
            
            //message should not be posted
            _commandProducer.MessageWasSent.Should().BeFalse();
            _eventProducer.MessageWasSent.Should().BeFalse();
            
            //message should correspond to the command
            var depositedPost = _fakeOutbox.Get(_message.Id);
            depositedPost.Id.Should().Be(_message.Id);
            depositedPost.Body.Value.Should().Be(_message.Body.Value);
            depositedPost.Header.Topic.Should().Be(_message.Header.Topic);
            depositedPost.Header.MessageType.Should().Be(_message.Header.MessageType);
            
            var depositedPost2 = _fakeOutbox.Get(_messageTwo.Id);
            depositedPost2.Id.Should().Be(_messageTwo.Id);
            depositedPost2.Body.Value.Should().Be(_messageTwo.Body.Value);
            depositedPost2.Header.Topic.Should().Be(_messageTwo.Header.Topic);
            depositedPost2.Header.MessageType.Should().Be(_messageTwo.Header.MessageType);
            
            var depositedPost3 = _fakeOutbox
                .OutstandingMessages(0)
                .SingleOrDefault(msg => msg.Id == _messageThree.Id);
            //message should correspond to the command
            depositedPost3.Id.Should().Be(_messageThree.Id);
            depositedPost3.Body.Value.Should().Be(_messageThree.Body.Value);
            depositedPost3.Header.Topic.Should().Be(_messageThree.Header.Topic);
            depositedPost3.Header.MessageType.Should().Be(_messageThree.Header.MessageType);
            
            //message should be marked as outstanding if not sent
            var outstandingMessages = _fakeOutbox.OutstandingMessages(0);
            outstandingMessages.Count().Should().Be(3);
        }
        
        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
