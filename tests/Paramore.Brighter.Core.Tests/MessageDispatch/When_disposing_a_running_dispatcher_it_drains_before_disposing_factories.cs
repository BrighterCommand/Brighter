#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{
    /// <summary>
    /// Regression for PR #4254 review finding 1 (message loss at shutdown), fixed at the ordering layer.
    ///
    /// The leak fix made <c>Dispatcher.Dispose()</c> cascade disposal to the mapper/transform factories. But
    /// <c>Dispose()</c> did not first stop the pumps, so a container teardown that raced a still-running pump
    /// (the host's ShutdownTimeout elapsed before drain, or the provider was disposed without a graceful stop)
    /// tore the factories down under an in-flight message — which surfaced as an <c>ObjectDisposedException</c>,
    /// was reclassified as Unacceptable, and the good message was rejected and discarded.
    ///
    /// The fix runs the graceful stop as part of disposal: <c>Dispose()</c> calls <c>End()</c> — which pushes a
    /// quit onto each pump so it runs out its current message, acknowledges it, and stops — waits for the pumps
    /// to drain (bounded by <c>ShutdownTimeout</c>), and only then disposes the factories. This drives a running
    /// dispatcher and asserts that by the time an owned factory is disposed the dispatcher has already stopped.
    /// Proved RED against the pre-fix <c>Dispose()</c> that disposed the factories while the pumps still ran.
    /// </summary>
    public class DispatcherDisposalDrainsPumpsTests : IDisposable
    {
        private const string Topic = "MyTopic";
        private const string ChannelName = "myChannel";
        private readonly RoutingKey _routingKey = new(Topic);
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly Dispatcher _dispatcher;
        private readonly StateCapturingMapperFactory _mapperFactory;

        public DispatcherDisposalDrainsPumpsTests()
        {
            var bus = new InternalBus();
            IAmACommandProcessor commandProcessor = new SpyCommandProcessor();

            _mapperFactory = new StateCapturingMapperFactory(() => new MyEventMessageMapper());
            var messageMapperRegistry = new MessageMapperRegistry(_mapperFactory, null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var subscription = new Subscription<MyEvent>(
                new SubscriptionName("test"),
                noOfPerformers: 1,
                timeOut: TimeSpan.FromMilliseconds(1000),
                channelFactory: new InMemoryChannelFactory(bus, _timeProvider, loggerFactory: Initializer.TestLoggerFactory),
                channelName: new ChannelName(ChannelName),
                messagePumpType: MessagePumpType.Reactor,
                routingKey: _routingKey);

            _dispatcher = new Dispatcher(
                commandProcessor,
                new List<Subscription> { subscription },
                Initializer.TestLoggerFactory, messageMapperRegistry,
                ownsRegistry: true);

            //the factory can only read the dispatcher's state at dispose time once the dispatcher exists
            _mapperFactory.ReadDispatcherState = () => _dispatcher.State;

            var message = new MyEventMessageMapper().MapToMessage(new MyEvent(), new Publication { Topic = _routingKey });
            for (var i = 0; i < 3; i++)
                bus.Enqueue(message);

            _dispatcher.Receive();
        }

        [Fact]
        public async Task When_disposing_a_running_dispatcher_it_drains_the_pumps_before_disposing_factories()
        {
            //give the running pump a moment to come up and start consuming
            await Task.Delay(500);

            _dispatcher.Dispose();

            //the pumps were stopped (End ran) as part of Dispose, not left running under a disposed factory
            Assert.Equal(DispatcherState.DS_STOPPED, _dispatcher.State);

            //and the owned mapper factory was disposed only after the dispatcher had stopped
            Assert.Equal(1, _mapperFactory.DisposeCount);
            Assert.Equal(DispatcherState.DS_STOPPED, _mapperFactory.StateAtDispose);
        }

        public void Dispose()
        {
            if (_dispatcher.State == DispatcherState.DS_RUNNING)
                _dispatcher.End().Wait();
        }

        private sealed class StateCapturingMapperFactory(Func<IAmAMessageMapper> make) : IAmAMessageMapperFactory, IDisposable
        {
            public int DisposeCount { get; private set; }
            public DispatcherState? StateAtDispose { get; private set; }
            public Func<DispatcherState>? ReadDispatcherState { get; set; }

            public Lease<IAmAMessageMapper>? Create(Type messageMapperType) => new Lease<IAmAMessageMapper>(make());
            public void Release(Lease<IAmAMessageMapper>? lease) { }

            public void Dispose()
            {
                DisposeCount++;
                StateAtDispose = ReadDispatcherState?.Invoke();
            }
        }
    }
}
