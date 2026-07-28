using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor
{
    // Pins Proactor.TranslateMessage (Proactor.cs:545) releasing the mapper the async unwrap pipeline owns.
    // TranslateMessage builds the pipeline from a reflectively-obtained object? handle and disposes it via
    // `await using var pipelineLifetime = pipeline as IAsyncDisposable;`. That `as` degrades silently to a
    // no-op `await using (null)` if a refactor changes what MakeUnwrapPipeline returns — a slow leak, not a
    // failure — so consuming a message must be shown to release every mapper it creates, deterministically.
    public class ProactorConsumeMapperReleaseTests
    {
        private const string ChannelName = "myChannel";
        private readonly RoutingKey _routingKey = new("MyTopic");
        private readonly InternalBus _bus = new();
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly IAmAMessagePump _messagePump;
        private readonly MyEvent _myEvent = new();
        private readonly ReleaseTrackingMessageMapperFactoryAsync _mapperFactory = new();

        public ProactorConsumeMapperReleaseTests()
        {
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.RegisterAsync<MyEvent, MyEventHandlerAsyncWithContinuation>();

            var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyEventHandlerAsyncWithContinuation());

            var commandProcessor = new CommandProcessor(
                subscriberRegistry,
                handlerFactory,
                new InMemoryRequestContextFactory(),
                new PolicyRegistry(),
                new ResiliencePipelineRegistry<string>(),
                new InMemorySchedulerFactory());

            PipelineBuilder<MyEvent>.ClearPipelineCache();

            var channel = new ChannelAsync(new(ChannelName), _routingKey,
                new InMemoryMessageConsumer(_routingKey, _bus, _timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000)));

            var messageMapperRegistry = new MessageMapperRegistry(null, _mapperFactory);
            messageMapperRegistry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();

            _messagePump = new ServiceActivator.Proactor(commandProcessor, _ => typeof(MyEvent),
                messageMapperRegistry, new EmptyMessageTransformerFactoryAsync(), new InMemoryRequestContextFactory(), channel)
            {
                Channel = channel, TimeOut = TimeSpan.FromMilliseconds(5000)
            };

            var message = new Message(
                new MessageHeader(_myEvent.Id, _routingKey, MessageType.MT_EVENT),
                new MessageBody(JsonSerializer.Serialize(_myEvent)));

            channel.Enqueue(message);
            channel.Enqueue(MessageFactory.CreateQuitMessage(_routingKey));
        }

        [Fact]
        public void When_consuming_a_message_the_proactor_releases_every_mapper_it_creates()
        {
            //act
            _messagePump.Run();

            //assert — no GC is forced: release must be deterministic, driven by TranslateMessage's
            //`await using` disposing the pipeline it built, not by the ~TransformPipelineAsync finalizer
            Assert.True(_mapperFactory.CreateCount > 0);
            Assert.Equal(_mapperFactory.CreateCount, _mapperFactory.ReleaseCount);
        }

        // Counts mappers handed out against mappers handed back. The async pipeline releases through
        // ReleaseAsync on the Proactor pump; Release is counted too so the assertion holds whichever the
        // disposal path uses. A mapper created but never returned is one the factory must retain — for an
        // IoC-backed factory, along with the scope it was resolved from — until process shutdown.
        private sealed class ReleaseTrackingMessageMapperFactoryAsync : IAmAMessageMapperFactoryAsync
        {
            private int _createCount;
            private int _releaseCount;

            public int CreateCount => Volatile.Read(ref _createCount);
            public int ReleaseCount => Volatile.Read(ref _releaseCount);

            public Lease<IAmAMessageMapperAsync>? Create(Type messageMapperType)
            {
                Interlocked.Increment(ref _createCount);
                return new Lease<IAmAMessageMapperAsync>(new MyEventMessageMapperAsync());
            }

            public void Release(Lease<IAmAMessageMapperAsync> lease)
            {
                Interlocked.Increment(ref _releaseCount);
            }

            public ValueTask ReleaseAsync(Lease<IAmAMessageMapperAsync> lease)
            {
                Interlocked.Increment(ref _releaseCount);
                return default;
            }
        }
    }
}
