using System;
using Newtonsoft.Json;
using NUnit.Framework;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using Polly;
using Polly.CircuitBreaker;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandProcessors
{
    [TestFixture]
    public class CircuitBreakerTests
    {
        private CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();
        private Message _message;
        private FakeMessageStore _messageStore;
        private FakeErroringMessageProducer _messagingProducer;
        private Exception _failedException;
        private BrokenCircuitException _circuitBrokenException;

        [SetUp]
        public void Establish()
        {
            _myCommand.Value = "Hello World";
            _messageStore = new FakeMessageStore();

            _messagingProducer = new FakeErroringMessageProducer();
            _message = new Message(
                header: new MessageHeader(messageId: _myCommand.Id, topic: "MyCommand", messageType: MessageType.MT_COMMAND),
                body: new MessageBody(JsonConvert.SerializeObject(_myCommand))
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

        [Test]
        public void When_An_Error_Should_Break_The_Circuit()
        {
            //break circuit with retries
            _failedException = Catch.Exception(() => _commandProcessor.Post(_myCommand));

            //now respond with broken ciruit
            _circuitBrokenException = (BrokenCircuitException)Catch.Exception(() => _commandProcessor.Post(_myCommand));

            Assert.AreEqual(4, _messagingProducer.SentCalledCount);
            Assert.IsInstanceOf(typeof(Exception), _failedException);
            Assert.IsInstanceOf(typeof(BrokenCircuitException), _circuitBrokenException);
        }

        [TearDown]
        public void Cleanup()
        {
            _commandProcessor.Dispose();
       }
   }
}