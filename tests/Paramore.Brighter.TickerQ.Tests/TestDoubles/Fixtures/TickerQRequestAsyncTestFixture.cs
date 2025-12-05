using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Paramore.Brighter.TickerQ.Tests.TestDoubles;
using Paramore.Brighter.TickerQ.Tests.TestDoubles.Fixtures;



namespace Paramore.Brighter.TickerQ.Tests
{
    public class TickerQRequestAsyncTestFixture : BaseTickerQFixture
    {
        protected override IAmAHandlerFactory GetHandlerFactory()
        {
            return new SimpleHandlerFactoryAsync(
               type =>
               {
                   if (type == typeof(MyEventHandlerAsync))
                   {
                       return new MyEventHandlerAsync(ReceivedMessages);
                   }

                   return new FireSchedulerRequestHandler(Processor!);
               });
        }

        protected override IAmAMessageMapperRegistry GetMapperRegistery()
        {
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
                new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));

            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
            return messageMapperRegistry;
        }

        protected override IAmASubscriberRegistry GetSubscriberServiceRegistry()
        {
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.RegisterAsync<MyEvent, MyEventHandlerAsync>();
            subscriberRegistry.RegisterAsync<FireSchedulerRequest, FireSchedulerRequestHandler>();

            return subscriberRegistry;
        }
    }
}
