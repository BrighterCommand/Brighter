using System;
using System.Threading.Tasks;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.TestHelpers;
using paramore.commandprocessor.tests.MessageDispatch.TestDoubles;

namespace paramore.commandprocessor.tests.MessageDispatch
{
    public class When_a_message_fails_to_be_mapped_to_a_request
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

            s_channel.Send(unmappableMessage);
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