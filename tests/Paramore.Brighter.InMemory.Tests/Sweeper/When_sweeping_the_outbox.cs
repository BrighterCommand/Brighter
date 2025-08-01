using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.InMemory.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Sweeper
{
    [Trait("Category", "InMemory")]
    [Collection("CommandProcess")]
    public class SweeperTests
    {
        private const string MyTopic = "MyTopic";

        [Fact]
        public async Task When_outstanding_in_outbox_sweep_clears_them()
        {
            //Arrange
            var timeSinceSent = TimeSpan.FromMilliseconds(6000);

            var timeProvider = new FakeTimeProvider();
            var tracer = new BrighterTracer(timeProvider);
            var internalBus = new InternalBus();
            var outbox = new InMemoryOutbox(timeProvider) { Tracer = new BrighterTracer(timeProvider) };

            var routingKey = new RoutingKey(MyTopic);

            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                {
                    routingKey, new InMemoryMessageProducer(internalBus, timeProvider, new Publication { RequestType = typeof(MyEvent), Topic = routingKey })
                }
            });

            var mapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
                new SimpleMessageMapperFactoryAsync(_ =>  new MyEventMessageMapperAsync())
            );

            mapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry,
                new DefaultPolicy(),
                mapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                outbox
            );

            CommandProcessor.ClearServiceBus();

            var commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                new PolicyRegistry(),
                mediator,
                new InMemorySchedulerFactory());

            var sweeper = new OutboxSweeper(timeSinceSent, mediator, new InMemoryRequestContextFactory());

            var events = new[]
            {
                new MyEvent{Value = "one"}, new MyEvent{Value = "two"}, new MyEvent{Value = "three"}
            };

            foreach (var e in events)
            {
                commandProcessor.Post(e);
            }

            //Act
            timeProvider.Advance(timeSinceSent); // -- let the messages expire

            await sweeper.SweepAsync();

            await Task.Delay(1000); //Give the sweep time to run

            //Assert
            Assert.Equal(3, internalBus.Stream(routingKey).Count());
            Assert.Empty(await outbox.OutstandingMessagesAsync(TimeSpan.Zero, new RequestContext()));
        }

        [Fact]
        public async Task When_outstanding_in_outbox_sweep_clears_them_async()
        {
            //Arrange
            var timeSinceSent = TimeSpan.FromMilliseconds(6000);

            var timeProvider = new FakeTimeProvider();

            var tracer = new BrighterTracer(timeProvider);
            var internalBus = new InternalBus();
            var outbox = new InMemoryOutbox(timeProvider) { Tracer = new BrighterTracer(timeProvider) };

            var routingKey = new RoutingKey(MyTopic);

            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                {
                    routingKey, new InMemoryMessageProducer(internalBus, timeProvider, new Publication { RequestType = typeof(MyEvent), Topic = routingKey })
                }
            });

            var mapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
                new SimpleMessageMapperFactoryAsync(_ =>  new MyEventMessageMapperAsync())
            );

            mapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry,
                new DefaultPolicy(),
                mapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                outbox
            );

            CommandProcessor.ClearServiceBus();

            var commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                new PolicyRegistry(),
                mediator,
                new InMemorySchedulerFactory());
            
            var sweeper = new OutboxSweeper(timeSinceSent, mediator, new InMemoryRequestContextFactory());

            var events = new[]
            {
                new MyEvent{Value = "one"}, new MyEvent{Value = "two"}, new MyEvent{Value = "three"}
            };

            foreach (var e in events)
            {
                commandProcessor.Post(e);
            }

            //Act
            timeProvider.Advance(timeSinceSent); // -- let the messages expire

            await sweeper.SweepAsync();

            await Task.Delay(1000); //Give the sweep time to run

            //Assert
            Assert.Equal(3, internalBus.Stream(routingKey).Count());
            Assert.Empty((await outbox.OutstandingMessagesAsync(TimeSpan.Zero, new RequestContext())));
        }

        [Fact]
        public async Task When_too_new_to_sweep_leaves_them()
        {
            //Arrange
            var timeSinceSent = TimeSpan.FromMilliseconds(6000);

            var timeProvider = new FakeTimeProvider();
            var tracer = new BrighterTracer(timeProvider);
            var internalBus = new InternalBus();
            var outbox = new InMemoryOutbox(timeProvider) { Tracer = new BrighterTracer(timeProvider) };

            var routingKey = new RoutingKey(MyTopic);

            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                {
                    routingKey, new InMemoryMessageProducer(internalBus, timeProvider, new Publication { RequestType = typeof(MyEvent), Topic = routingKey })
                }
            });

            var mapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
                new SimpleMessageMapperFactoryAsync(_ =>  new MyEventMessageMapperAsync())
            );

            mapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry,
                new DefaultPolicy(),
                mapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                outbox
            );

            CommandProcessor.ClearServiceBus();

            var commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                new PolicyRegistry(),
                mediator,
                new InMemorySchedulerFactory());
            
            var sweeper = new OutboxSweeper(
                timeSinceSent,
                mediator,
                new InMemoryRequestContextFactory());

            var oldEvent = new MyEvent{Value = "old"};
            commandProcessor.DepositPost(oldEvent);

            //delay so the previous message is old enough to sweep
            timeProvider.Advance(timeSinceSent);

            var events = new[]
            {
                new MyEvent{Value = "one"}, new MyEvent{Value = "two"}, new MyEvent{Value = "three"}
            };

            foreach (var e in events)
            {
                commandProcessor.DepositPost(e);
            }

            //Act

            await sweeper.SweepAsync();

            await Task.Delay(1000); //Give the sweep time to run

            //Assert
            Assert.Single(internalBus.Stream(routingKey));
            Assert.Equal(3, outbox.OutstandingMessages(TimeSpan.Zero, new RequestContext()).Count());
        }

        [Fact]
        public async Task When_too_new_to_sweep_leaves_them_async()
        {
            //Arrange
            var timeSinceSent = TimeSpan.FromMilliseconds(6000);

            var timeProvider = new FakeTimeProvider();

            var tracer = new BrighterTracer(timeProvider);
            var internalBus = new InternalBus();
            var outbox = new InMemoryOutbox(timeProvider) { Tracer = new BrighterTracer(timeProvider) };

            var routingKey = new RoutingKey(MyTopic);

            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                {
                    routingKey, new InMemoryMessageProducer(internalBus, timeProvider, new Publication { RequestType = typeof(MyEvent), Topic = routingKey })
                }
            });

            var mapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
                new SimpleMessageMapperFactoryAsync(_ =>  new MyEventMessageMapperAsync())
            );

            mapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry,
                new DefaultPolicy(),
                mapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                outbox
            );

            CommandProcessor.ClearServiceBus();

            var commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                new PolicyRegistry(),
                mediator,
                new InMemorySchedulerFactory());           
            
            var sweeper = new OutboxSweeper(timeSinceSent, mediator, new InMemoryRequestContextFactory());

            var oldEvent = new MyEvent{Value = "old"};
            commandProcessor.DepositPost(oldEvent);

            //-- allow the messages to be old enough to sweep
            timeProvider.Advance(timeSinceSent);

            var events = new[]
            {
                new MyEvent{Value = "one"}, new MyEvent{Value = "two"}, new MyEvent{Value = "three"}
            };

            foreach (var e in events)
            {
                commandProcessor.DepositPost(e);
            }

            //Act
            await sweeper.SweepAsync();

            await Task.Delay(1000); //Give the sweep time to run

            //Assert
            Assert.Single(internalBus.Stream(routingKey));
            Assert.Equal(3, (await outbox.OutstandingMessagesAsync(TimeSpan.Zero, new RequestContext())).Count());
        }
    }
}
