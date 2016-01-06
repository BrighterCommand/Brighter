using System;
using FakeItEasy;
using Machine.Specifications;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using Polly;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    public class When_Using_Decoupled_Invocation_To_Send_A_Message_Asynchronously
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();
        private static Message s_message;
        private static FakeMessageStore s_fakeMessageStore;
        private static FakeMessageProducer s_fakeMessageProducer;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_myCommand.Value = "Hello World";

            s_fakeMessageStore = new FakeMessageStore();
            s_fakeMessageProducer = new FakeMessageProducer();

            s_message = new Message(
                header: new MessageHeader(messageId: s_myCommand.Id, topic: "MyCommand", messageType: MessageType.MT_COMMAND),
                body: new MessageBody(JsonConvert.SerializeObject(s_myCommand))
                );

            var messageMapperRegistry = new MessageMapperRegistry(new TestMessageMapperFactory(() => new MyCommandMessageMapper()));
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            s_commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                new PolicyRegistry() { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } },
                messageMapperRegistry,
                s_fakeMessageStore,
                s_fakeMessageProducer,
                logger);
        };

        private Because _of = () => s_commandProcessor.Post(s_myCommand);

        private Cleanup cleanup = () => s_commandProcessor.Dispose();

        private It _should_store_the_message_in_the_sent_command_message_repository = () => s_fakeMessageStore.MessageWasAdded.ShouldBeTrue();
        private It _should_send_a_message_via_the_messaging_gateway = () => s_fakeMessageProducer.MessageWasSent.ShouldBeTrue();
    }
}