#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{
    /// <summary>
    /// Regression for PR #4254 review finding 1, the timeout branch. The drain-on-dispose wait is bounded by
    /// <see cref="Dispatcher.ShutdownTimeout"/>: a pump that cannot finish its in-flight message in time must not
    /// block <c>Dispose()</c> indefinitely — disposal proceeds regardless (leaving the message un-acknowledged
    /// for the broker to redeliver) so container teardown is never wedged by a stuck handler.
    ///
    /// A handler that blocks forever wedges the pump: the quit <c>End()</c> pushes cannot be picked up while the
    /// pump thread is parked in dispatch, so the control task never completes. With a short
    /// <see cref="Dispatcher.ShutdownTimeout"/> this exercises the <c>End().Wait(timeout)</c> returning
    /// <c>false</c> path. The test runs <c>Dispose()</c> on a dedicated thread and joins with a generous bound so
    /// a regression that reinstated an unbounded wait fails fast as a <c>Join</c> timeout rather than hanging the
    /// run. Proved RED against an <c>End().Wait()</c> with no timeout argument.
    /// </summary>
    public class DispatcherDisposeHonoursShutdownTimeoutTests : IDisposable
    {
        private const string Topic = "MyTopic";
        private const string ChannelName = "myChannel";
        private readonly RoutingKey _routingKey = new(Topic);
        private readonly FakeTimeProvider _timeProvider = new();
        private readonly Dispatcher _dispatcher;
        private readonly BlockingCommandProcessor _commandProcessor = new();
        private readonly DisposeCountingTransformerFactory _transformerFactory = new();

        public DispatcherDisposeHonoursShutdownTimeoutTests()
        {
            var bus = new InternalBus();

            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()), null);
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var subscription = new Subscription<MyEvent>(
                new SubscriptionName("test"),
                noOfPerformers: 1,
                timeOut: TimeSpan.FromMilliseconds(1000),
                channelFactory: new InMemoryChannelFactory(bus, _timeProvider),
                channelName: new ChannelName(ChannelName),
                messagePumpType: MessagePumpType.Reactor,
                routingKey: _routingKey);

            _dispatcher = new Dispatcher(
                _commandProcessor,
                new List<Subscription> { subscription },
                messageMapperRegistry,
                messageTransformerFactory: _transformerFactory,
                ownsTransformerFactories: true,
                shutdownTimeout: TimeSpan.FromMilliseconds(250));

            var message = new MyEventMessageMapper().MapToMessage(new MyEvent(), new Publication { Topic = _routingKey });
            bus.Enqueue(message);

            _dispatcher.Receive();
        }

        [Fact]
        public void When_a_pump_will_not_drain_in_time_dispose_still_returns_and_disposes_the_factories()
        {
            //wait until a message is in flight and the handler is wedged, so End()'s quit cannot be picked up
            Assert.True(_commandProcessor.PublishEntered.Wait(TimeSpan.FromSeconds(5)), "the pump never reached the handler");

            var disposeReturned = new ManualResetEventSlim(false);
            var disposeThread = new Thread(() =>
            {
                _dispatcher.Dispose();
                disposeReturned.Set();
            }) { IsBackground = true };
            disposeThread.Start();

            //the 250ms drain times out and Dispose returns; a regression to an unbounded wait would hang here
            Assert.True(disposeReturned.Wait(TimeSpan.FromSeconds(5)), "Dispose did not return within the bound — the shutdown timeout was not honoured");

            //disposal proceeded past the timed-out drain and tore down the owned factory
            Assert.Equal(1, _transformerFactory.DisposeCount);
        }

        public void Dispose()
        {
            //let the wedged handler go so the pump can unwind, then stop the dispatcher if it is still running
            _commandProcessor.ReleasePublish.Set();
            if (_dispatcher.State == DispatcherState.DS_RUNNING)
                _dispatcher.End().Wait(TimeSpan.FromSeconds(5));
        }

        //blocks the pump thread inside dispatch so the in-flight message cannot finish and the pump cannot pick
        //up the quit End() pushes — standing in for a long-running handler that outlasts the shutdown timeout
        private sealed class BlockingCommandProcessor : SpyCommandProcessor
        {
            public ManualResetEventSlim PublishEntered { get; } = new(false);
            public ManualResetEventSlim ReleasePublish { get; } = new(false);

            public override void Publish<T>(T @event, RequestContext? requestContext = null)
            {
                PublishEntered.Set();
                ReleasePublish.Wait();
            }
        }

        private sealed class DisposeCountingTransformerFactory : IAmAMessageTransformerFactory, IDisposable
        {
            public int DisposeCount { get; private set; }
            public Lease<IAmAMessageTransform>? Create(Type transformerType) => null;
            public void Release(Lease<IAmAMessageTransform>? lease) { }
            public void Dispose() => DisposeCount++;
        }
    }
}
