using System;
using System.Diagnostics;
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using nUnitShouldAdapter;

namespace paramore.commandprocessor.tests.MessagingGateway
{
    [Subject(typeof(Channel))]
    public class When_Requeuing_A_Message_With_Supported_But_Disabled_Delay
    {
        private static IAmAChannel s_channel;
        private static IAmAMessageConsumerSupportingDelay s_gateway;
        private static Message s_requeueMessage;
        private static Stopwatch s_stopWatch;

        private Establish _context = () =>
        {
            s_gateway = A.Fake<IAmAMessageConsumerSupportingDelay>();
            A.CallTo(() => s_gateway.DelaySupported).Returns(false);

            s_channel = new Channel("test", s_gateway);

            s_requeueMessage = new Message(
                new MessageHeader(Guid.NewGuid(), "key", MessageType.MT_EVENT),
                new MessageBody("a test body"));

            s_stopWatch = new Stopwatch();
        };

        private Because _of = () =>
        {
            s_stopWatch.Start();
            s_channel.Requeue(s_requeueMessage, 1000);
            s_stopWatch.Stop();
        };

        private It _should_call_the_messaging_gateway = () => A.CallTo(() => s_gateway.Requeue(s_requeueMessage)).MustHaveHappened();
        private It _should_have_process_delayed_the_call = () => (s_stopWatch.ElapsedMilliseconds > 900).ShouldBeTrue();
    }
}