﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    [Collection("CommandProcessor")]
    public class CommandProcessorDepositPostTests : IDisposable
    {
        
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly Message _message;
        private readonly FakeOutbox _fakeOutbox;
        private readonly FakeMessageProducerWithPublishConfirmation _producer;

        public CommandProcessorDepositPostTests()
        {
            const string topic = "MyCommand";
            _myCommand.Value = "Hello World";

            _producer = new FakeMessageProducerWithPublishConfirmation{Publication = {Topic = new RoutingKey(topic), RequestType = typeof(MyCommand)}};

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
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            var producerRegistry =
                new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
                {
                    { topic, _producer },
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
            var postedMessageId = _commandProcessor.DepositPost(_myCommand);
            
            //assert
            
            //message should not be posted
            _producer.MessageWasSent.Should().BeFalse();
            
            //message should correspond to the command
            var depositedPost = _fakeOutbox.Get(postedMessageId);
            depositedPost.Id.Should().Be(_message.Id);
            depositedPost.Body.Value.Should().Be(_message.Body.Value);
            depositedPost.Header.Topic.Should().Be(_message.Header.Topic);
            depositedPost.Header.MessageType.Should().Be(_message.Header.MessageType);
            
            //message should be marked as outstanding if not sent
            var outstandingMessages = _fakeOutbox.OutstandingMessages(0);
            var outstandingMessage = outstandingMessages.Single();
            outstandingMessage.Id.Should().Be(_message.Id);
        }
        
        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
