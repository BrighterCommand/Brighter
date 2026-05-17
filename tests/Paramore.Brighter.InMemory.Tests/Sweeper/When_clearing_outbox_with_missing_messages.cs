using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.InMemory.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Sweeper
{
    [Trait("Category", "InMemory")]
    [Collection("CommandProcess")]
    public class ClearOutboxMissingMessagesTests
    {
        private const string MyTopic = "MyTopic";

        [Fact]
        public void When_clearing_outbox_with_missing_messages_should_dispatch_found()
        {
            //Arrange
            var timeProvider = new FakeTimeProvider();
            var tracer = new BrighterTracer(timeProvider);
            var internalBus = new InternalBus();
            var outbox = new InMemoryOutbox(timeProvider) { Tracer = new BrighterTracer(timeProvider) };

            var routingKey = new RoutingKey(MyTopic);

            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                {
                    routingKey, new InMemoryMessageProducer(internalBus, new Publication { RequestType = typeof(MyEvent), Topic = routingKey })
                }
            });

            var mapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
                new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync())
            );

            mapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry,
                new Polly.Registry.ResiliencePipelineRegistry<string>().AddBrighterDefault(),
                mapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                outbox
            );

            var context = new RequestContext();

            //Add two messages directly to the outbox
            var message1Id = Guid.NewGuid().ToString();
            var message2Id = Guid.NewGuid().ToString();
            var missingId = Guid.NewGuid().ToString();

            var message1 = new Message(
                new MessageHeader(message1Id, routingKey, MessageType.MT_EVENT),
                new MessageBody("body one"));
            var message2 = new Message(
                new MessageHeader(message2Id, routingKey, MessageType.MT_EVENT),
                new MessageBody("body two"));

            outbox.Add(message1, context);
            outbox.Add(message2, context);

            //Delete one message to simulate compaction loss
            outbox.Delete(new Id[] { new(message1Id) }, context);

            //Act — clear with all three IDs including the missing one
            var allIds = new Id[] { new(message1Id), new(message2Id), new(missingId) };

            var exception = Record.Exception(() => mediator.ClearOutbox(allIds, context));

            //Assert — no exception thrown
            Assert.Null(exception);

            //The found message should have been dispatched
            Assert.Single(internalBus.Stream(routingKey));
        }

        [Fact]
        public async Task When_clearing_outbox_async_with_missing_messages_should_dispatch_found()
        {
            //Arrange
            var timeProvider = new FakeTimeProvider();
            var tracer = new BrighterTracer(timeProvider);
            var internalBus = new InternalBus();
            var outbox = new InMemoryOutbox(timeProvider) { Tracer = new BrighterTracer(timeProvider) };

            var routingKey = new RoutingKey(MyTopic);

            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                {
                    routingKey, new InMemoryMessageProducer(internalBus, new Publication { RequestType = typeof(MyEvent), Topic = routingKey })
                }
            });

            var mapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
                new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync())
            );

            mapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry,
                new Polly.Registry.ResiliencePipelineRegistry<string>().AddBrighterDefault(),
                mapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                outbox
            );

            var context = new RequestContext();

            //Add two messages directly to the outbox
            var message1Id = Guid.NewGuid().ToString();
            var message2Id = Guid.NewGuid().ToString();
            var missingId = Guid.NewGuid().ToString();

            var message1 = new Message(
                new MessageHeader(message1Id, routingKey, MessageType.MT_EVENT),
                new MessageBody("body one"));
            var message2 = new Message(
                new MessageHeader(message2Id, routingKey, MessageType.MT_EVENT),
                new MessageBody("body two"));

            outbox.Add(message1, context);
            outbox.Add(message2, context);

            //Delete one message to simulate compaction loss
            outbox.Delete(new Id[] { new(message1Id) }, context);

            //Act — clear with all three IDs including the missing one
            var allIds = new Id[] { new(message1Id), new(message2Id), new(missingId) };

            var exception = await Record.ExceptionAsync(() => mediator.ClearOutboxAsync(allIds, context));

            //Assert — no exception thrown
            Assert.Null(exception);

            //The found message should have been dispatched
            Assert.Single(internalBus.Stream(routingKey));
        }
    }
}
