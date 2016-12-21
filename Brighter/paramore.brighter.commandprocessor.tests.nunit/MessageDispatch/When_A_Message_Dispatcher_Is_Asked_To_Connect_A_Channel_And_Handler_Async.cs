using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using nUnitShouldAdapter;
using NUnit.Specifications;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using paramore.brighter.commandprocessor.tests.nunit.MessageDispatch.TestDoubles;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.TestHelpers;

namespace paramore.brighter.commandprocessor.tests.nunit.MessageDispatch
{
    public class When_A_Message_Dispatcher_Is_Asked_To_Connect_A_Channel_And_Handler_Async : ContextSpecification
    {
        private static Dispatcher s_dispatcher;
        private static FakeChannel s_channel;
        private static SpyCommandProcessor s_commandProcessor;

        private Establish _context = () =>
        {
            s_channel = new FakeChannel();
            s_commandProcessor = new SpyCommandProcessor();

            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(() => new MyEventMessageMapper()));
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var connection = new Connection(
                name: new ConnectionName("test"), 
                dataType: typeof(MyEvent), 
                noOfPerformers: 1, 
                timeoutInMilliseconds: 1000, 
                channelFactory: new InMemoryChannelFactory(s_channel), 
                channelName: new ChannelName("fakeChannel"), 
                routingKey: "fakekey",
                isAsync: true);
            s_dispatcher = new Dispatcher(s_commandProcessor, messageMapperRegistry, new List<Connection> { connection });

            var @event = new MyEvent();
            var message = new MyEventMessageMapper().MapToMessage(@event);
            s_channel.Add(message);

            s_dispatcher.State.ShouldEqual(DispatcherState.DS_AWAITING);
            s_dispatcher.Receive();
        };


        private Because _of = () =>
        {
            Task.Delay(1000).Wait();
            s_dispatcher.End().Wait();
        };

        private It _should_have_consumed_the_messages_in_the_channel = () => s_channel.Length.ShouldEqual(0);
        private It _should_have_a_stopped_state = () => s_dispatcher.State.ShouldEqual(DispatcherState.DS_STOPPED);
        private It _should_have_dispatched_a_request = () => s_commandProcessor.Observe<MyEvent>().ShouldNotBeNull();
        private It _should_have_published_async = () => s_commandProcessor.Commands.Any(ctype => ctype == CommandType.PublishAsync).ShouldBeTrue();

    }
}
