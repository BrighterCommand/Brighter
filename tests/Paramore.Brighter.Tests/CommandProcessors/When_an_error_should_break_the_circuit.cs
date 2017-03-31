using System;
using FluentAssertions;
using Newtonsoft.Json;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Polly;
using Polly.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Tests.CommandProcessors
{
    public class CircuitBreakerTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private Message _message;
        private readonly FakeMessageStore _messageStore;
        private readonly FakeErroringMessageProducer _messagingProducer;
        private Exception _failedException;
        private BrokenCircuitException _circuitBrokenException;

        public CircuitBreakerTests()
        {
            _myCommand.Value = "Hello World";
            _messageStore = new FakeMessageStore();

            _messagingProducer = new FakeErroringMessageProducer();
            _message = new Message(
                new MessageHeader(_myCommand.Id, "MyCommand", MessageType.MT_COMMAND),
                new MessageBody(JsonConvert.SerializeObject(_myCommand))
                );
            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(() => new MyCommandMessageMapper()));
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetry(new[]
                {
                    TimeSpan.FromMilliseconds(50),
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(150)
                });

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMinutes(1));

            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                new PolicyRegistry { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } },
                messageMapperRegistry,
                _messageStore,
                _messagingProducer);
        }

        [Fact]
        public void When_An_Error_Should_Break_The_Circuit()
        {
            //break circuit with retries
            _failedException = Catch.Exception(() => _commandProcessor.Post(_myCommand));

            //now respond with broken ciruit
            _circuitBrokenException = (BrokenCircuitException)Catch.Exception(() => _commandProcessor.Post(_myCommand));

            _messagingProducer.SentCalledCount.Should().Be(4);
            _failedException.Should().BeOfType<Exception>();
            _circuitBrokenException.Should().BeOfType<BrokenCircuitException>();
        }

        public void Dispose()
        {
            _commandProcessor.Dispose();
       }
   }
}