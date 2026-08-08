#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{
    /// <summary>
    /// Regression for PR #4254 review finding 2. <c>Dispatcher.Dispose()</c> drains the pumps with a blocking
    /// <c>End().Wait(ShutdownTimeout)</c> — sync-over-async in a disposal path, the exact shape this PR argued
    /// against elsewhere. A host that disposes its provider via <c>IAsyncDisposable</c> (which MS DI honours in
    /// preference to <c>IDisposable</c>) should be able to <b>await</b> the shutdown drain rather than park a
    /// thread on it. So <see cref="Dispatcher"/> also implements <see cref="IAsyncDisposable"/>: the graceful
    /// path awaits the drain, then async-disposes an owned factory that is itself <see cref="IAsyncDisposable"/>.
    ///
    /// This drives a running dispatcher, awaits <c>DisposeAsync</c>, and asserts (a) the pumps drained before the
    /// owned transformer factory was disposed (the ordering that is the whole point — the factory reads the
    /// dispatcher state at dispose time and sees it already <c>DS_STOPPED</c>), and (b) the factory was torn down
    /// via its async-dispose path, not the blocking one. Proved RED against a Dispatcher with no <c>DisposeAsync</c>.
    /// </summary>
    public class DispatcherAsyncDisposalDrainsPumpsTests : IDisposable
    {
        private const string Topic = "MyTopic";
        private const string ChannelName = "myChannel";
        private readonly RoutingKey _routingKey = new(Topic);
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly Dispatcher _dispatcher;
        private readonly AsyncStateCapturingTransformerFactory _transformerFactory;

        public DispatcherAsyncDisposalDrainsPumpsTests()
        {
            var bus = new InternalBus();
            IAmACommandProcessor commandProcessor = new SpyCommandProcessor();

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()), null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            //the transformer factory is disposed directly by the Dispatcher (the mapper factory would cascade
            //through the sync-only registry), so it is where the async-dispose path is observable
            _transformerFactory = new AsyncStateCapturingTransformerFactory();

            var subscription = new Subscription<MyEvent>(
                new SubscriptionName("test"),
                noOfPerformers: 1,
                timeOut: TimeSpan.FromMilliseconds(1000),
                channelFactory: new InMemoryChannelFactory(bus, _timeProvider),
                channelName: new ChannelName(ChannelName),
                messagePumpType: MessagePumpType.Reactor,
                routingKey: _routingKey);

            _dispatcher = new Dispatcher(
                commandProcessor,
                new List<Subscription> { subscription },
                messageMapperRegistry,
                messageTransformerFactory: _transformerFactory,
                ownsTransformerFactories: true);

            //the factory can only read the dispatcher's state at dispose time once the dispatcher exists
            _transformerFactory.ReadDispatcherState = () => _dispatcher.State;

            var message = new MyEventMessageMapper().MapToMessage(new MyEvent(), new Publication { Topic = _routingKey });
            for (var i = 0; i < 3; i++)
                bus.Enqueue(message);

            _dispatcher.Receive();
        }

        [Fact]
        public async Task When_async_disposing_a_running_dispatcher_it_drains_the_pumps_before_disposing_factories()
        {
            //give the running pump a moment to come up and start consuming
            await Task.Delay(500);

            await _dispatcher.DisposeAsync();

            //the pumps were stopped (End ran) as part of DisposeAsync, not left running under a disposed factory
            Assert.Equal(DispatcherState.DS_STOPPED, _dispatcher.State);

            //and the owned transformer factory was disposed only after the dispatcher had stopped
            Assert.Equal(DispatcherState.DS_STOPPED, _transformerFactory.StateAtDispose);

            //the async path tore the factory down via DisposeAsync, not the blocking Dispose
            Assert.Equal(1, _transformerFactory.DisposeAsyncCount);
            Assert.Equal(0, _transformerFactory.DisposeCount);
        }

        public void Dispose()
        {
            if (_dispatcher.State == DispatcherState.DS_RUNNING)
                _dispatcher.End().Wait();
        }

        private sealed class AsyncStateCapturingTransformerFactory
            : IAmAMessageTransformerFactory, IDisposable, IAsyncDisposable
        {
            public int DisposeCount { get; private set; }
            public int DisposeAsyncCount { get; private set; }
            public DispatcherState? StateAtDispose { get; private set; }
            public Func<DispatcherState>? ReadDispatcherState { get; set; }

            //MyEventMessageMapper carries no transform attributes, so Create is never asked for a transform;
            //only the factory's disposal at teardown matters here
            public Lease<IAmAMessageTransform>? Create(Type transformerType) => null;
            public void Release(Lease<IAmAMessageTransform>? lease) { }

            public void Dispose()
            {
                DisposeCount++;
                StateAtDispose ??= ReadDispatcherState?.Invoke();
            }

            public ValueTask DisposeAsync()
            {
                DisposeAsyncCount++;
                StateAtDispose ??= ReadDispatcherState?.Invoke();
                return default;
            }
        }
    }
}
