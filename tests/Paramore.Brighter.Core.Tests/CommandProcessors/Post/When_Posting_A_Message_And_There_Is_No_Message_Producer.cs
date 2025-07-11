using System;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
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
    public class CommandProcessorPostMissingMessageProducerTests : IDisposable
    {
        private readonly MyCommand _myCommand = new MyCommand();
        private readonly InMemoryOutbox _outbox;
        private Exception _exception;
        private readonly MessageMapperRegistry _messageMapperRegistry;
        private readonly RetryPolicy _retryPolicy;
        private readonly CircuitBreakerPolicy _circuitBreakerPolicy;
        private readonly IAmABrighterTracer _tracer;

        public CommandProcessorPostMissingMessageProducerTests()
        {
            _myCommand.Value = "Hello World";

            var timeProvider = new FakeTimeProvider();
            _tracer = new BrighterTracer(timeProvider);
            _outbox = new InMemoryOutbox(timeProvider) {Tracer = _tracer};

            _messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
                null);
            _messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            _retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            _circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public void When_Creating_A_Command_Processor_Without_Producer_Registry()
        {                                             
            var policyRegistry = new PolicyRegistry { { CommandProcessor.RETRYPOLICY, _retryPolicy }, { CommandProcessor.CIRCUITBREAKER, _circuitBreakerPolicy } };

            _exception = Catch.Exception(() => new OutboxProducerMediator<Message, CommittableTransaction>(
                null, 
                policyRegistry,
                _messageMapperRegistry,
                 new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                _tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                circuitBreaker: new InMemoryCircuitBreaker(),
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
