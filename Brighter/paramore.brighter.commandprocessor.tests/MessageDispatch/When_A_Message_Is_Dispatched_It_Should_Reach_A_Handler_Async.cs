using System;
using FakeItEasy;
using Machine.Specifications;
using Newtonsoft.Json;
using Nito.AsyncEx;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.TestHelpers;
using paramore.commandprocessor.tests.CommandProcessors;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using paramore.commandprocessor.tests.MessageDispatch.TestDoubles;

namespace paramore.commandprocessor.tests.MessageDispatch
{
    public class When_A_Message_Is_Dispatched_It_Should_Reach_A_Handler_Async
    {
        private static IAmAMessagePump s_messagePump;
        private static FakeChannel s_channel;
        private static IAmACommandProcessor s_commandProcessor;
        private static MyEvent s_event;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.RegisterAsync<MyEvent, MyEventHandlerAsyncWithContinuation>();

            s_commandProcessor = new CommandProcessor(
                subscriberRegistry,
                new CheapHandlerFactoryAsync(), 
                new InMemoryRequestContextFactory(), 
                new PolicyRegistry(), 
                logger);

            s_channel = new FakeChannel();
            var mapper = new MyEventMessageMapper();
            s_messagePump = new MessagePumpAsync<MyEvent>(s_commandProcessor, mapper) { Channel = s_channel, TimeoutInMilliseconds = 5000 };

            s_event = new MyEvent();

            var message = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonConvert.SerializeObject(s_event)));
            s_channel.Add(message);
            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            s_channel.Add(quitMessage);
        };
        
        private Because _of = () => AsyncContext.Run(async () => await s_messagePump.Run());

        private It _should_dispatch_the_message_to_a_handler = () => MyEventHandlerAsyncWithContinuation.ShouldReceive(s_event).ShouldBeTrue();
        private It _should_modify_the_loop_counter_on_the_callback_thread = () => MyEventHandlerAsyncWithContinuation.LoopCounter.Value.ShouldEqual(2);
        private It _should_use_the_same_thread_for_the_continuation = () => MyEventHandlerAsyncWithContinuation.WorkThreadId.ShouldEqual(MyEventHandlerAsyncWithContinuation.ContinuationThreadId);


        internal class CheapHandlerFactoryAsync : IAmAHandlerFactoryAsync
        {
            public IHandleRequestsAsync Create(Type handlerType)
            {
                var logger = A.Fake<ILog>();
                if (handlerType == typeof(MyEventHandlerAsyncWithContinuation))
                {
                    return new MyEventHandlerAsyncWithContinuation(logger);
                }
                return null;
            }

            public void Release(IHandleRequestsAsync handler)
            {
                var disposable = handler as IDisposable;
                if (disposable != null)
                    disposable.Dispose();
            }
        }
    }
}
