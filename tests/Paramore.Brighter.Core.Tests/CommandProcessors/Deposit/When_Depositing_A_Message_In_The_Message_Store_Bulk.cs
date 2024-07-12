﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Transactions;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Deposit
{
    [Collection("CommandProcessor")]
    public class CommandProcessorBulkDepositPostTests : IDisposable
    {
        private const string CommandTopic = "MyCommand";
        private const string EventTopic = "MyEvent";

        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new();
        private readonly MyCommand _myCommandTwo = new();
        private readonly MyEvent _myEvent = new();
        private readonly Message _message;
        private readonly Message _messageTwo;
        private readonly Message _messageThree;
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _bus = new();

        public CommandProcessorBulkDepositPostTests()
        {
            _myCommand.Value = "Hello World";

            var timeProvider = new FakeTimeProvider();
            InMemoryProducer commandProducer = new(_bus, timeProvider);
            commandProducer.Publication = new Publication 
            { 
                Topic = new RoutingKey(CommandTopic), 
                RequestType = typeof(MyCommand) 
            };

            InMemoryProducer eventProducer = new(_bus, timeProvider);
            eventProducer.Publication = new Publication 
            { 
                Topic = new RoutingKey(EventTopic), 
                RequestType = typeof(MyEvent) 
            };
            
            _message = new Message(
                new MessageHeader(_myCommand.Id, CommandTopic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
                );
            
            _messageTwo = new Message(
                new MessageHeader(_myCommandTwo.Id, CommandTopic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommandTwo, JsonSerialisationOptions.Options))
            );
            
            _messageThree = new Message(
                new MessageHeader(_myEvent.Id, EventTopic, MessageType.MT_EVENT),
                new MessageBody(JsonSerializer.Serialize(_myEvent, JsonSerialisationOptions.Options))
            );

            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((type) =>
            {
                if (type == typeof(MyCommandMessageMapper))
                    return new MyCommandMessageMapper();
                else if (type == typeof(MyEventMessageMapper))
                    return new MyEventMessageMapper();
                
                throw new ConfigurationException($"No command or event mappers registered for {type.Name}");
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
                { CommandTopic, commandProducer },
                { EventTopic, eventProducer}
            });

            var policyRegistry = new PolicyRegistry
            {
                { CommandProcessor.RETRYPOLICY, retryPolicy },
                { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy }
            };

            var tracer = new BrighterTracer();
            _outbox = new InMemoryOutbox(timeProvider) {Tracer = tracer};
            
            IAmAnExternalBusService bus = new ExternalBusService<Message, CommittableTransaction>(
                producerRegistry, 
                policyRegistry,
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                _outbox
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
            var context = new RequestContext();
            
            //assert
            
            //message should not be posted
            
            _bus.Stream(new RoutingKey(CommandTopic)).Any().Should().BeFalse();
            _bus.Stream(new RoutingKey(EventTopic)).Any().Should().BeFalse();
            
            //message should correspond to the command
            var depositedPost = _outbox.Get(_message.Id, context);
            depositedPost.Id.Should().Be(_message.Id);
            depositedPost.Body.Value.Should().Be(_message.Body.Value);
            depositedPost.Header.Topic.Should().Be(_message.Header.Topic);
            depositedPost.Header.MessageType.Should().Be(_message.Header.MessageType);
            
            var depositedPost2 = _outbox.Get(_messageTwo.Id, context);
            depositedPost2.Id.Should().Be(_messageTwo.Id);
            depositedPost2.Body.Value.Should().Be(_messageTwo.Body.Value);
            depositedPost2.Header.Topic.Should().Be(_messageTwo.Header.Topic);
            depositedPost2.Header.MessageType.Should().Be(_messageTwo.Header.MessageType);
            
            var depositedPost3 = _outbox
                .OutstandingMessages(0, context)
                .SingleOrDefault(msg => msg.Id == _messageThree.Id);
            //message should correspond to the command
            depositedPost3.Id.Should().Be(_messageThree.Id);
            depositedPost3.Body.Value.Should().Be(_messageThree.Body.Value);
            depositedPost3.Header.Topic.Should().Be(_messageThree.Header.Topic);
            depositedPost3.Header.MessageType.Should().Be(_messageThree.Header.MessageType);
            
            //message should be marked as outstanding if not sent
            var outstandingMessages = _outbox.OutstandingMessages(0, context);
            outstandingMessages.Count().Should().Be(3);
        }
        
        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
