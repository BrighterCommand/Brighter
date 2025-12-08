using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;



namespace Paramore.Brighter.TickerQ.Tests.TestDoubles.Fixtures
{
    public class TickerQRequestTestFixture : BaseTickerQFixture
    {
        protected override IAmAMessageMapperRegistry GetMapperRegistery()
        {
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
                null);

            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();
            return messageMapperRegistry;
        }

        protected override IAmASubscriberRegistry GetSubscriberServiceRegistry()
        {
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyEvent, MyEventHandler>();
            subscriberRegistry.RegisterAsync<FireSchedulerRequest, FireSchedulerRequestHandler>();
            return subscriberRegistry;
        }

        protected override IAmAHandlerFactory GetHandlerFactory()
        {
            return new SimpleHandlerFactory(
               _ => new MyEventHandler(ReceivedMessages),
               _ => new FireSchedulerRequestHandler(Processor!));
        }

    }
}
