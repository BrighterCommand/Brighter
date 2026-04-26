using System.Collections.Concurrent;
using System.Linq;
using System.Transactions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.MessageScheduler.TickerQ;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Polly;
using Polly.Registry;
using TickerQ.DependencyInjection;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Interfaces;
using TickerQ.Utilities.Interfaces.Managers;
using TUnit.Core.Interfaces;

namespace Paramore.Brighter.TickerQ.Tests.TestDoubles
{
    /// <summary>
    /// Process-wide TickerQ test host. TickerQ relies on a static
    /// <c>TickerFunctionProvider</c> populated by <c>TickerQInitializerHostedService</c>,
    /// which only runs via the <see cref="IHost"/> lifecycle when <c>UseTickerQ()</c>
    /// has been called. We therefore build a single <see cref="WebApplication"/>
    /// (it implements both <c>IHost</c> and <c>IApplicationBuilder</c>, so
    /// <c>UseTickerQ</c> resolves on every TickerQ version), share it across the
    /// assembly via <see cref="SharedType.PerAssembly"/>, and let each test isolate
    /// its assertions by the unique <see cref="Id"/> of the request/message it
    /// scheduled — entries in the shared <see cref="ReceivedMessages"/> map are
    /// keyed by that Id.
    /// </summary>
    public sealed class TickerQTestHost : IAsyncInitializer, IAsyncDisposable
    {
        public static readonly TimeSpan EventuallyTimeout = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan EventuallyPollInterval = TimeSpan.FromMilliseconds(250);

        private WebApplication? _host;
        private readonly Dictionary<Type, RoutingKey> _routingKeysByEventType;

        public IAmACommandProcessor Processor { get; private set; } = null!;
        public TickerQSchedulerFactory SchedulerFactory { get; private set; } = null!;
        public InMemoryOutbox Outbox { get; private set; } = null!;
        public InternalBus InternalBus { get; } = new();
        public RoutingKey RoutingKey { get; } = new($"TickerQ-Tests-{Guid.NewGuid():N}");
        public RoutingKey RoutingKeySync { get; } = new($"TickerQ-Tests-Sync-{Guid.NewGuid():N}");
        public TimeProvider TimeProvider { get; } = TimeProvider.System;
        public ConcurrentDictionary<string, string> ReceivedMessages { get; } = new();

        public TickerQTestHost()
        {
            _routingKeysByEventType = new Dictionary<Type, RoutingKey>
            {
                [typeof(MyEvent)] = RoutingKey,
                [typeof(MyEventSync)] = RoutingKeySync,
            };
        }

        public RoutingKey RoutingKeyFor<TEvent>() => _routingKeysByEventType[typeof(TEvent)];

        public Message BuildMessage<TEvent>() where TEvent : Event, new()
        {
            var req = new TEvent();
            return new Message(
                new MessageHeader { MessageId = req.Id, MessageType = MessageType.MT_EVENT, Topic = RoutingKeyFor<TEvent>() },
                new MessageBody(System.Text.Json.JsonSerializer.Serialize(req)));
        }

        public bool BusContains<TEvent>(string id) =>
            InternalBus.Stream(RoutingKeyFor<TEvent>()).Any(m => m.Id == id);

        public async Task EventuallyHandled(string id) =>
            await Assert.That(() => ReceivedMessages.ContainsKey(id))
                .Eventually(s => s.IsTrue(), EventuallyTimeout, EventuallyPollInterval);

        public async Task EventuallyOnBus<TEvent>(string id) =>
            await Assert.That(() => BusContains<TEvent>(id))
                .Eventually(s => s.IsTrue(), EventuallyTimeout, EventuallyPollInterval);

        public async Task AssertOutboxEmptyForId(string id)
        {
            var expected = Message.Empty;
            var actual = await Outbox.GetAsync(id, new RequestContext());
            await Assert.That(actual.Body).IsEquivalentTo(expected.Body);
            await Assert.That(actual.Id).IsEqualTo(expected.Id);
            await Assert.That(actual.Persist).IsEqualTo(expected.Persist);
            await Assert.That(actual.Redelivered).IsEqualTo(expected.Redelivered);
            await Assert.That(actual.DeliveryTag).IsEqualTo(expected.DeliveryTag);
            await Assert.That(actual.Header.MessageType).IsEqualTo(expected.Header.MessageType);
            await Assert.That(actual.Header.Topic).IsEqualTo(expected.Header.Topic);
            await Assert.That(actual.Header.TimeStamp).IsEqualTo(expected.Header.TimeStamp).Within(TimeSpan.FromSeconds(1));
            await Assert.That(actual.Header.CorrelationId).IsEqualTo(expected.Header.CorrelationId);
            await Assert.That(actual.Header.ReplyTo).IsEqualTo(expected.Header.ReplyTo);
            await Assert.That(actual.Header.ContentType).IsEqualTo(expected.Header.ContentType);
            await Assert.That(actual.Header.HandledCount).IsEqualTo(expected.Header.HandledCount);
        }

        public async Task InitializeAsync()
        {
            // MyEvent uses the async handler, MyEventSync uses the sync handler:
            // Brighter's pipeline does not cleanly support both sync and async handlers
            // on the same event type.
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyEventSync, MyEventHandler>();
            subscriberRegistry.RegisterAsync<MyEvent, MyEventHandlerAsync>();
            subscriberRegistry.RegisterAsync<FireSchedulerRequest, FireSchedulerRequestHandler>();
            subscriberRegistry.RegisterAsync<FireSchedulerMessage, FireSchedulerMessageHandler>();

            CommandProcessor? processor = null;
            var handlerFactory = new SimpleHandlerFactory(
                type =>
                {
                    if (type == typeof(MyEventHandler))
                        return new MyEventHandler(ReceivedMessages);
                    throw new InvalidOperationException($"No sync handler for {type}");
                },
                type =>
                {
                    if (type == typeof(MyEventHandlerAsync))
                        return new MyEventHandlerAsync(ReceivedMessages);
                    if (type == typeof(FireSchedulerRequestHandler))
                        return new FireSchedulerRequestHandler(processor!);
                    if (type == typeof(FireSchedulerMessageHandler))
                        return new FireSchedulerMessageHandler(processor!);
                    throw new InvalidOperationException($"No async handler for {type}");
                });

            var policyRegistry = new PolicyRegistry
            {
                [CommandProcessor.RETRYPOLICY] = Policy.Handle<Exception>().Retry(),
                [CommandProcessor.CIRCUITBREAKER] =
                    Policy.Handle<Exception>().CircuitBreaker(1, TimeSpan.FromMilliseconds(1))
            };

            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                [RoutingKey] = new InMemoryMessageProducer(InternalBus, new Publication { Topic = RoutingKey, RequestType = typeof(MyEvent) }),
                [RoutingKeySync] = new InMemoryMessageProducer(InternalBus, new Publication { Topic = RoutingKeySync, RequestType = typeof(MyEventSync) })
            });

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(type =>
                {
                    if (type == typeof(JsonMessageMapper<MyEvent>)) return new JsonMessageMapper<MyEvent>();
                    if (type == typeof(JsonMessageMapper<MyEventSync>)) return new JsonMessageMapper<MyEventSync>();
                    throw new InvalidOperationException($"No sync mapper for {type}");
                }),
                new SimpleMessageMapperFactoryAsync(_ => new JsonMessageMapperAsync<MyEvent>()));
            messageMapperRegistry.Register<MyEvent, JsonMessageMapper<MyEvent>>();
            messageMapperRegistry.Register<MyEventSync, JsonMessageMapper<MyEventSync>>();
            messageMapperRegistry.RegisterAsync<MyEvent, JsonMessageMapperAsync<MyEvent>>();

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
                Outbox);

            var builder = WebApplication.CreateBuilder();
            // Ephemeral port — tests don't make HTTP calls; default :5000 collides under
            // CI when other listeners or sibling test hosts are present.
            builder.WebHost.UseSetting("urls", "http://127.0.0.1:0");
            builder.Services.AddTickerQ(options =>
            {
                options.ConfigureScheduler(s =>
                {
                    s.MaxConcurrency = 64;
                    s.FallbackIntervalChecker = TimeSpan.FromMilliseconds(250);
                });
            });
            builder.Services.AddSingleton(TimeProvider);

            builder.Services.AddSingleton(sp => new TickerQSchedulerFactory(
                sp.GetRequiredService<ITimeTickerManager<TimeTickerEntity>>(),
                sp.GetRequiredService<ITickerPersistenceProvider<TimeTickerEntity, CronTickerEntity>>(),
                TimeProvider));
            builder.Services.AddSingleton<IAmAMessageSchedulerFactory>(sp => sp.GetRequiredService<TickerQSchedulerFactory>());
            builder.Services.AddSingleton<IAmARequestSchedulerFactory>(sp => sp.GetRequiredService<TickerQSchedulerFactory>());

            builder.Services.AddSingleton<IAmACommandProcessor>(sp =>
            {
                processor = new CommandProcessor(
                    subscriberRegistry,
                    handlerFactory,
                    new InMemoryRequestContextFactory(),
                    policyRegistry,
                    new ResiliencePipelineRegistry<string>(),
                    outboxBus,
                    sp.GetRequiredService<TickerQSchedulerFactory>());
                return processor;
            });

            _host = builder.Build();

            Processor = _host.Services.GetRequiredService<IAmACommandProcessor>();
            SchedulerFactory = _host.Services.GetRequiredService<TickerQSchedulerFactory>();

            // UseTickerQ flips InitializationRequested so TickerQInitializerHostedService
            // runs TickerFunctionProvider.Build() and seeding during host start.
            _host.UseTickerQ();
            await _host.StartAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_host is not null)
            {
                await _host.StopAsync();
                await _host.DisposeAsync();
            }
        }
    }
}
