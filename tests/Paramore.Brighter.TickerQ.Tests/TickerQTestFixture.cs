using System;
using System.Text.Json;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.MessageScheduler.TickerQ;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Paramore.Brighter.TickerQ.Tests.TestDoubles;
using ParamoreBrighter.TickerQ.Tests.TestDoubles;
using Polly;
using Polly.Registry;
using TickerQ.DependencyInjection;
using TickerQ.DependencyInjection.Hosting;
using TickerQ.Utilities;
using TickerQ.Utilities.Base;
using TickerQ.Utilities.Interfaces;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Models.Ticker;


namespace ParamoreBrighter.TickerQ.Tests
{
    public class TickerQTestFixture
    {
        public TickerQSchedulerFactory SchedulerFactory { get; }
        public IAmACommandProcessor Processor { get; }
        public InMemoryOutbox Outbox { get; }
        public InternalBus InternalBus { get; } = new();

        public RoutingKey RoutingKey { get; }
        public TimeProvider TimeProvider { get; }

        private readonly IServiceCollection _serviceCollection;
        public ServiceProvider ServiceProvider { get; }

        public TickerQTestFixture()
        {

            _serviceCollection = new ServiceCollection();
            _serviceCollection.AddLogging();
            _serviceCollection.AddTickerQ(opt =>
            {

            });

            RoutingKey = new RoutingKey($"Test-{Guid.NewGuid():N}");
            TimeProvider = TimeProvider.System;

            var handlerFactory = new SimpleHandlerFactory(
                _ => new MyEventHandler(new Dictionary<string, string>()),
                _ => new FireSchedulerMessageHandler(Processor!));

            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyEvent, MyEventHandler>();
            subscriberRegistry.RegisterAsync<FireSchedulerMessage, FireSchedulerMessageHandler>();

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

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
                null);

            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

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



            _serviceCollection.AddSingleton<TickerQSchedulerFactory>(sp =>
            {
                var tickerManager = sp.GetRequiredService<ITimeTickerManager<TimeTicker>>();
                return new TickerQSchedulerFactory(tickerManager);
            });

            _serviceCollection.AddSingleton<IAmAMessageSchedulerFactory>(sp =>
                sp.GetRequiredService<TickerQSchedulerFactory>());

            _serviceCollection.AddSingleton<IAmARequestSchedulerFactory>(sp =>
                sp.GetRequiredService<TickerQSchedulerFactory>());

            _serviceCollection.AddSingleton<IAmACommandProcessor>(sp =>
            {
                var tickermanager = sp.GetRequiredService<ITimeTickerManager<TimeTicker>>();
                var scheduler = new TickerQSchedulerFactory(tickermanager);
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


            var appBuilder = new MyApplicationBuilder
            {
                ApplicationServices = ServiceProvider
            };
            appBuilder.UseTickerQ();
        }
    }
}
