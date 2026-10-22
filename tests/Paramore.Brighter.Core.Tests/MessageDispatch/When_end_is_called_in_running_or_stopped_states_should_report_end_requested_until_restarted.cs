#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */


#endregion

#nullable enable
#nullable enable

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch;

public class DispatcherEndRequestedUntilRestartedTests
{
    [Fact]
    public async Task When_end_is_called_in_running_or_stopped_states_should_report_end_requested_until_restarted()
    {
        // Arrange
        using var channelFactory = new InMemoryUnopenedConsumerChannelFactory(throwOnDispose: false);
        var registry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
        registry.Register<MyEvent, MyEventMessageMapper>();
        registry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
        using var dispatcher = new Dispatcher(new SpyCommandProcessor(), [NewSubscription(channelFactory)], registry, registry);
        var disposedDispatcher = new Dispatcher(new SpyCommandProcessor(), [NewSubscription(channelFactory)], registry, registry);

        try
        {
            // Act
            dispatcher.Receive();
            var endInRunning = dispatcher.End();
            var afterEndInRunning = dispatcher.GetLifecycleSnapshot();
            await endInRunning.WaitAsync(TimeSpan.FromSeconds(10));

            dispatcher.Receive();
            foreach (var consumer in dispatcher.Consumers.ToArray())
                consumer.Shut(consumer.Subscription.RoutingKey);
            WaitUntilStopped(dispatcher);
            var stoppedBeforeEnd = dispatcher.GetLifecycleSnapshot();
            await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));
            var afterEndInStopped = dispatcher.GetLifecycleSnapshot();

            dispatcher.Receive();
            var afterRestart = dispatcher.GetLifecycleSnapshot();

            disposedDispatcher.Receive();
            disposedDispatcher.Dispose();
            var afterDispose = disposedDispatcher.GetLifecycleSnapshot();

            // Assert
            Assert.True(afterEndInRunning.EndRequested);
            Assert.Equal(DispatcherState.DS_STOPPED, stoppedBeforeEnd.State);
            Assert.False(stoppedBeforeEnd.EndRequested);
            Assert.Equal(DispatcherState.DS_STOPPED, afterEndInStopped.State);
            Assert.True(afterEndInStopped.EndRequested);
            Assert.True(afterDispose.EndRequested);
            Assert.Equal(DispatcherState.DS_RUNNING, afterRestart.State);
            Assert.False(afterRestart.EndRequested);
        }
        finally
        {
            await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));
            disposedDispatcher.Dispose();
        }
    }

    private static void WaitUntilStopped(Dispatcher dispatcher)
    {
        var stopwatch = Stopwatch.StartNew();
        while (dispatcher.State != DispatcherState.DS_STOPPED && stopwatch.Elapsed < TimeSpan.FromSeconds(10))
            Thread.Sleep(10);
    }

    private static Subscription NewSubscription(IAmAChannelFactory channelFactory)
        => new Subscription<MyEvent>(
            new SubscriptionName("alpha"),
            channelName: new ChannelName("alpha-channel"),
            routingKey: new RoutingKey("alpha-topic"),
            channelFactory: channelFactory,
            messagePumpType: MessagePumpType.Reactor,
            noOfPerformers: 1,
            timeOut: TimeSpan.FromMilliseconds(10),
            emptyChannelDelay: TimeSpan.FromMilliseconds(10));
}
