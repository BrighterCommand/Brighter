using System;
using System.Threading.Tasks;
using nUnitShouldAdapter;
using NUnit.Specifications;
using paramore.brighter.commandprocessor.tests.nunit.MessageDispatch.TestDoubles;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.TestHelpers;

namespace paramore.brighter.commandprocessor.tests.nunit.MessageDispatch
{
    [Subject(typeof(MessagePump<>))]
    public class When_A_Message_Fails_To_Be_Mapped_To_A_Request : ContextSpecification
    {
        private static IAmAMessagePump s_messagePump;
        private static FakeChannel s_channel;
        private static SpyRequeueCommandProcessor s_commandProcessor;

        private Establish context = () =>
        {
            s_commandProcessor = new SpyRequeueCommandProcessor();
            s_channel = new FakeChannel();
            var mapper = new FailingEventMessageMapper();
            s_messagePump = new MessagePump<MyFailingMapperEvent>(s_commandProcessor, mapper) { Channel = s_channel, TimeoutInMilliseconds = 5000, RequeueCount = 3, UnacceptableMessageLimit = 3 };

            var unmappableMessage = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody("{ \"Id\" : \"48213ADB-A085-4AFF-A42C-CF8209350CF7\" }"));

            s_channel.Add(unmappableMessage);
        };

        private Because of = () =>
        {
            var task = Task.Factory.StartNew(() => s_messagePump.Run(), TaskCreationOptions.LongRunning);
            Task.Delay(1000).Wait();

            s_channel.Stop();

            Task.WaitAll(new[] { task });
        };

        private It should_have_acknowledge_the_message = () => s_channel.AcknowledgeHappened.ShouldBeTrue();
    }
}