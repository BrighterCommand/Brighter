using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Reactor
{
    // Pins Reactor.TranslateMessage (Reactor.cs:535) releasing the mapper the unwrap pipeline owns.
    // TranslateMessage builds the pipeline from a reflectively-obtained object? handle and disposes it via
    // `using var pipelineLifetime = pipeline as IDisposable;`. That `as` degrades silently to a no-op
    // `using (null)` if a refactor changes what MakeUnwrapPipeline returns — a slow leak, not a failure —
    // so consuming a message must be shown to release every mapper it creates, deterministically (no GC).
    public class ReactorConsumeMapperReleaseTests
    {
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly MyEvent _myEvent = new();
        private readonly ReleaseTrackingMessageMapperFactory _mapperFactory = new();

        public ReactorConsumeMapperReleaseTests()
        {
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyEvent, MyEventHandler>();

            var handlerFactory = new SimpleHandlerFactorySync(_ => new MyEventHandler(new Dictionary<string, string>()));

            var commandProcessor = new CommandProcessor(
                subscriberRegistry,
                handlerFactory,
                new InMemoryRequestContextFactory(),
                new PolicyRegistry(),
                new ResiliencePipelineRegistry<string>(),
                new InMemorySchedulerFactory());

            PipelineBuilder<MyEvent>.ClearPipelineCache();

            var channel = new Channel(
                new("myChannel"), _routingKey,
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000))
            );

            var messageMapperRegistry = new MessageMapperRegistry(_mapperFactory, null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            _messagePump = new ServiceActivator.Reactor(commandProcessor, _ => typeof(MyEvent),
                messageMapperRegistry, null, new InMemoryRequestContextFactory(), channel)
            {
                Channel = channel, TimeOut = TimeSpan.FromMilliseconds(5000)
            };

            var message = new Message(
                new MessageHeader(_myEvent.Id, _routingKey, MessageType.MT_EVENT),
                new MessageBody(JsonSerializer.Serialize(_myEvent, JsonSerialisationOptions.Options))
            );

            channel.Enqueue(message);
            channel.Enqueue(MessageFactory.CreateQuitMessage(_routingKey));
        }

        [Fact]
        public void When_consuming_a_message_the_reactor_releases_every_mapper_it_creates()
        {
            //act
            _messagePump.Run();

            //assert — no GC is forced: release must be deterministic, driven by TranslateMessage disposing
            //the pipeline it built, not by the ~TransformPipeline finalizer
            Assert.True(_mapperFactory.CreateCount > 0);
            Assert.Equal(_mapperFactory.CreateCount, _mapperFactory.ReleaseCount);
        }

        // Counts mappers handed out against mappers handed back. A mapper the pump creates but never
        // returns is one the factory must retain — for an IoC-backed factory, along with the scope it was
        // resolved from — until process shutdown.
        private sealed class ReleaseTrackingMessageMapperFactory : IAmAMessageMapperFactory
        {
            private int _createCount;
            private int _releaseCount;

            public int CreateCount => Volatile.Read(ref _createCount);
            public int ReleaseCount => Volatile.Read(ref _releaseCount);

            public IAmAMessageMapper Create(Type messageMapperType)
            {
                Interlocked.Increment(ref _createCount);
                return new MyEventMessageMapper();
            }

            public void Release(IAmAMessageMapper mapper)
            {
                Interlocked.Increment(ref _releaseCount);
            }
        }
    }
}
