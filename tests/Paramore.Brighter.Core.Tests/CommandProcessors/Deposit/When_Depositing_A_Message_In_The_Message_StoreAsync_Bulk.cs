﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
    public class CommandProcessorBulkDepositPostTestsAsync: IDisposable
    {
        private const string CommandTopic = "MyCommand";
        private const string EventTopic = "MyEvent";

        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new();
        private readonly MyCommand _myCommand2 = new();
        private readonly MyEvent _myEvent = new();
        private readonly Message _message;
        private readonly Message _message2;
        private readonly Message _message3;
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _internalBus = new();

        public CommandProcessorBulkDepositPostTestsAsync()
        {
            _myCommand.Value = "Hello World";
            _myCommand2.Value = "Update World";
            
            var timeProvider = new FakeTimeProvider();

            InMemoryProducer commandProducer = new(_internalBus, timeProvider);
            commandProducer.Publication = new Publication
            {
                Topic =  new RoutingKey(CommandTopic),
                RequestType = typeof(MyCommand)
            };

            InMemoryProducer eventProducer = new(_internalBus, timeProvider);
            eventProducer.Publication = new Publication
            {
                Topic =  new RoutingKey(EventTopic),
                RequestType = typeof(MyEvent)
            };
            
            _message = new Message(
                new MessageHeader(_myCommand.Id, CommandTopic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
                );
            
            _message2 = new Message(
                new MessageHeader(_myCommand2.Id, CommandTopic, MessageType.MT_COMMAND),
                new MessageBody(JsonSerializer.Serialize(_myCommand2, JsonSerialisationOptions.Options))
            );
            
            _message3 = new Message(
                new MessageHeader(_myEvent.Id, EventTopic, MessageType.MT_EVENT),
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
                    { CommandTopic, commandProducer },
                    { EventTopic, eventProducer }
                }); 
            
            var tracer = new BrighterTracer(new FakeTimeProvider());
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
        public async Task When_depositing_a_message_in_the_outbox()
        {
            //act
            var context = new RequestContext();
            var requests = new List<IRequest> {_myCommand, _myCommand2, _myEvent } ;
            await _commandProcessor.DepositPostAsync(requests);
            
            
            //assert
            //message should not be posted
            _internalBus.Stream(new RoutingKey(CommandTopic)).Any().Should().BeFalse();
            _internalBus.Stream(new RoutingKey(EventTopic)).Any().Should().BeFalse();
            
            //message should be in the store
            var depositedPost = _outbox
                .OutstandingMessages(0, context)
                .SingleOrDefault(msg => msg.Id == _message.Id);
            
            //message should be in the store
            var depositedPost2 = _outbox
                .OutstandingMessages(0, context)
                .SingleOrDefault(msg => msg.Id == _message2.Id);
            
            //message should be in the store
            var depositedPost3 = _outbox
                .OutstandingMessages(0, context)
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
