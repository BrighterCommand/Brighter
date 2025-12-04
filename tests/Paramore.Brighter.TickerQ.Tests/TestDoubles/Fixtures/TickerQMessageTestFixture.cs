using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;



namespace Paramore.Brighter.TickerQ.Tests.TestDoubles.Fixtures
{
    public class TickerQMessageTestFixture : BaseTickerQFixture
    {
        protected override IAmAHandlerFactory GetHandlerFactory()
        {
            return new SimpleHandlerFactory(
                         _ => new MyEventHandler(new Dictionary<string, string>()),
                         _ => new FireSchedulerMessageHandler(Processor!));
        }

        protected override IAmASubscriberRegistry GetSubscriberServiceRegistry()
        {
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyEvent, MyEventHandler>();
            subscriberRegistry.RegisterAsync<FireSchedulerMessage, FireSchedulerMessageHandler>();

            return subscriberRegistry;
        }

        protected override IAmAMessageMapperRegistry GetMapperRegistery()
        {
            var messageMapperRegistry = new MessageMapperRegistry(
                 new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
                 new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));

            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            return messageMapperRegistry;
        }
    }
}
