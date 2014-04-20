using System;
using System.Threading.Tasks;
using Machine.Specifications;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.TestHelpers;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.MessageDispatcher.TestDoubles;

namespace paramore.commandprocessor.tests.MessageDispatcher
{
    public class When_running_a_message_pump_on_a_thread_should_be_able_to_stop
    {
        static Performer<MyEvent> performer;
        private static SpyCommandProcessor commandProcessor;
        private static InMemoryChannel channel;
        private static Task performerTask;

        private Establish context = () =>
        {
            commandProcessor = new SpyCommandProcessor();
            channel = new InMemoryChannel();
            var mapper = new MyEventMessageMapper();
            var messagePump = new MessagePump<MyEvent>(channel, commandProcessor, mapper, 40000);

            var @event = new MyEvent();
            var message = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonConvert.SerializeObject(@event)));
            channel.Enqueue(message);

            performer = new Performer<MyEvent>(channel, messagePump);
            performerTask = performer.Run();
            performer.Stop();
        };

        Because of = () => performerTask.Wait() ;
        It should_terminate_successfully = () => performerTask.IsCompleted.ShouldBeTrue();
        It should_not_have_errored = () => performerTask.IsFaulted.ShouldBeFalse();
        It should_not_show_as_cancelled = () => performerTask.IsCanceled.ShouldBeFalse();
        It should_have_consumed_the_messages_in_the_channel = () => channel.Length.ShouldEqual(0);
    }
}
