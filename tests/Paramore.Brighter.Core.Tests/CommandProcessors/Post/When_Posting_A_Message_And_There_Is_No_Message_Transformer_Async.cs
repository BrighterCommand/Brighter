﻿using System;
using System.Collections.Generic;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Observability;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Retry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post
{
    [Collection("CommandProcessor")]
    public class CommandProcessorPostMissingMessageTransformerTestsAsync : IDisposable
    {
        private readonly MyCommand _myCommand = new();
        private readonly InMemoryOutbox _outbox;
        private Exception _exception;
        private readonly MessageMapperRegistry _messageMapperRegistry;
        private readonly ProducerRegistry _producerRegistry;
        private readonly PolicyRegistry _policyRegistry;
        private readonly IAmABrighterTracer _tracer;

        public CommandProcessorPostMissingMessageTransformerTestsAsync()
        {
            _myCommand.Value = "Hello World";

            var timeProvider = new FakeTimeProvider();
            _tracer = new BrighterTracer(timeProvider);
            _outbox = new InMemoryOutbox(timeProvider) {Tracer = _tracer};

            _messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
                null);
            _messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            RetryPolicy retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            CircuitBreakerPolicy circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            var routingKey = new RoutingKey("MyTopic");
            
            _producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                {
                    routingKey, new InMemoryProducer(new InternalBus(), new FakeTimeProvider()){ 
                    Publication =
                    {
                        Topic = routingKey, RequestType = typeof(MyCommand)
                    }
                }},
            });

            _policyRegistry = new PolicyRegistry { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } };
         }

        [Fact]
        public void When_Creating_A_Command_Processor_Without_Message_Transformer_Async()
        {                                             
            _exception = Catch.Exception(() => new OutboxProducerMediator<Message, CommittableTransaction>(
                _producerRegistry, 
                _policyRegistry,
                _messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                null,
                _tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                _outbox)
            );               

            Assert.IsType<ConfigurationException>(_exception); 
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
