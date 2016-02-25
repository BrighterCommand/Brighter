using System;
using FakeItEasy;
using Machine.Specifications;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using Polly;
using Polly.CircuitBreaker;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    public class When_An_Error_Should_Break_The_Circuit
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();
        private static Message s_message;
        private static FakeMessageStore s_messageStore;
        private static FakeErroringMessageProducer s_messagingProducer;
        private static Exception s_failedException;
        private static BrokenCircuitException s_circuitBrokenException;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_myCommand.Value = "Hello World";
            s_messageStore = new FakeMessageStore();

            s_messagingProducer = new FakeErroringMessageProducer();
            s_message = new Message(
                header: new MessageHeader(messageId: s_myCommand.Id, topic: "MyCommand", messageType: MessageType.MT_COMMAND),
                body: new MessageBody(JsonConvert.SerializeObject(s_myCommand))
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


            s_commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                new PolicyRegistry() { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } },
                messageMapperRegistry,
                s_messageStore,
                s_messagingProducer,
                logger);
        };

        private Because _of = () =>
        {
            //break circuit with retries
            s_failedException = Catch.Exception(() => s_commandProcessor.Post(s_myCommand));
            //now resond with broken ciruit
            s_circuitBrokenException = (BrokenCircuitException)Catch.Exception(() => s_commandProcessor.Post(s_myCommand));
        };

        private Cleanup cleanup = () => s_commandProcessor.Dispose();

        private It _should_send_messages_via_the_messaging_gateway = () => s_messagingProducer.SentCalledCount.ShouldEqual(4);
        private It _should_throw_a_exception_out_once_all_retries_exhausted = () => s_failedException.ShouldBeOfExactType(typeof(Exception));
        private It _should_throw_a_circuit_broken_exception_once_circuit_broken = () => s_circuitBrokenException.ShouldBeOfExactType(typeof(BrokenCircuitException));
    }
}