using System;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.MessageScheduler.TickerQ;
using Paramore.Brighter.Observability;
using ParamoreBrighter.TickerQ.Tests.TestDoubles;
using Polly;
using Polly.Registry;
using TickerQ.DependencyInjection;
using TickerQ.DependencyInjection.Hosting;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Models.Ticker;

namespace Paramore.Brighter.TickerQ.Tests.TestDoubles.Fixtures
{
    public abstract class BaseTickerQFixture
    {
        public TickerQSchedulerFactory SchedulerFactory { get; }
        public IAmACommandProcessor Processor { get; }
        public InMemoryOutbox Outbox { get; }
        public InternalBus InternalBus { get; } = new();

        public RoutingKey RoutingKey { get; }
        public TimeProvider TimeProvider { get; }

        private readonly IServiceCollection _serviceCollection;
        public ServiceProvider ServiceProvider { get; }
        public Dictionary<string, string> ReceivedMessages { get; }

        protected BaseTickerQFixture()
        {
            ReceivedMessages = new();
            RoutingKey = new RoutingKey($"Test-{Guid.NewGuid():N}");
            TimeProvider = TimeProvider.System;
            _serviceCollection = new ServiceCollection();
            _serviceCollection.AddLogging();
            _serviceCollection.AddTickerQ();
            _serviceCollection.AddSingleton(TimeProvider);
            var handlerFactory = GetHandlerFactory();

            var subscriberRegistry = GetSubscriberServiceRegistry();

            var policyRegistry = new PolicyRegistry
            {
                [CommandProcessor.RETRYPOLICY] = Policy.Handle<Exception>().Retry(),
                [CommandProcessor.CIRCUITBREAKER] =
                    Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(1))
            };

            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                [RoutingKey] = new InMemoryMessageProducer(InternalBus, TimeProvider, new Publication { Topic = RoutingKey, RequestType = typeof(MyEvent) })
            });

            var messageMapperRegistry = GetMapperRegistery();

            var trace = new BrighterTracer(TimeProvider);
            Outbox = new InMemoryOutbox(TimeProvider) { Tracer = trace };

            var outboxBus = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry,
                new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                trace,
                new FindPublicationByPublicationTopicOrRequestType(),
                Outbox
            );

            _serviceCollection.AddSingleton(sp =>
            {
                var tickerManager = sp.GetRequiredService<ITimeTickerManager<TimeTicker>>();
                var timeProvider = sp.GetRequiredService<TimeProvider>();
                return new TickerQSchedulerFactory(tickerManager, timeProvider);
            });

            _serviceCollection.AddSingleton<IAmAMessageSchedulerFactory>(sp =>
                sp.GetRequiredService<TickerQSchedulerFactory>());

            _serviceCollection.AddSingleton<IAmARequestSchedulerFactory>(sp =>
                sp.GetRequiredService<TickerQSchedulerFactory>());

            _serviceCollection.AddSingleton<IAmACommandProcessor>(sp =>
            {
                var scheduler = sp.GetRequiredService<TickerQSchedulerFactory>();
                return new CommandProcessor(
               subscriberRegistry,
               handlerFactory,
               new InMemoryRequestContextFactory(),
               policyRegistry,
               new ResiliencePipelineRegistry<string>(),
               outboxBus,
               scheduler);
            });

            CommandProcessor.ClearServiceBus();
            ServiceProvider = _serviceCollection.BuildServiceProvider();
            Processor = ServiceProvider.GetRequiredService<IAmACommandProcessor>();
            SchedulerFactory = ServiceProvider.GetRequiredService<TickerQSchedulerFactory>();
            var myHost = new MyHost() { Services = ServiceProvider };
            myHost.UseTickerQ();
        }
        protected abstract IAmAHandlerFactory GetHandlerFactory();
        protected abstract IAmASubscriberRegistry GetSubscriberServiceRegistry();
        protected abstract IAmAMessageMapperRegistry GetMapperRegistery();

        public void Clear()
        {
            var length = InternalBus.Stream(RoutingKey).Count();
            for (int i = 0; i < length; i++)
            {
                InternalBus.Dequeue(RoutingKey);
            }
            ReceivedMessages.Clear();
        }
    }
}
