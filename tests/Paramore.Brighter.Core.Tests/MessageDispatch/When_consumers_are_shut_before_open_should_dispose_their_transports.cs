#region Licence

/* The MIT License (MIT)
Copyright © 2026 gabisonia <irakli.gabisonia94@gmail.com>

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

using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch;

public class DispatcherUnopenedConsumerDisposalTests : IObserver<LogEvent>
{
    private Dispatcher? _dispatcher;
    private IAmAConsumer[] _unopenedConsumers = [];

    [Theory]
    [InlineData(MessagePumpType.Reactor, 1, false)]
    [InlineData(MessagePumpType.Proactor, 1, false)]
    [InlineData(MessagePumpType.Reactor, 3, false)]
    [InlineData(MessagePumpType.Proactor, 3, false)]
    [InlineData(MessagePumpType.Reactor, 3, true)]
    [InlineData(MessagePumpType.Proactor, 3, true)]
    public async Task When_consumers_are_shut_before_open_should_dispose_their_transports(
        MessagePumpType pumpType, int performerCount, bool throwOnDispose)
    {
        // Arrange
        using var context = TestCorrelator.CreateContext();
        using var channelFactory = new InMemoryUnopenedConsumerChannelFactory(throwOnDispose);
        var registry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
        registry.Register<MyEvent, MyEventMessageMapper>();
        registry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
        var subscription = new Subscription<MyEvent>(
            new SubscriptionName("unopened-consumers"),
            channelName: new ChannelName("unopened-channel"),
            routingKey: new RoutingKey("unopened-topic"),
            channelFactory: channelFactory,
            messagePumpType: pumpType,
            noOfPerformers: performerCount,
            timeOut: TimeSpan.FromMilliseconds(10),
            emptyChannelDelay: TimeSpan.FromMilliseconds(10));
        using var dispatcher = new Dispatcher(new SpyCommandProcessor(), [subscription], registry, registry);
        _dispatcher = dispatcher;
        using var observer = TestCorrelator.GetLogEventStreamFromCurrentContext().Subscribe(this);

        try
        {
            // Act
            dispatcher.Receive();
            await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));

            // Assert
            Assert.Equal(performerCount, _unopenedConsumers.Length);
            Assert.Equal(performerCount, channelFactory.Transports.Count);
            Assert.All(_unopenedConsumers, consumer =>
            {
                Assert.Equal(ConsumerState.Shut, consumer.State);
                Assert.Null(consumer.Job);
                Assert.Equal(0, consumer.JobId);
            });
            Assert.All(channelFactory.Transports, transport =>
            {
                Assert.Equal(0, transport.ReceiveCount);
                Assert.Equal(1, transport.DisposeCount);
            });
            Assert.Empty(dispatcher.Consumers);
            Assert.Equal(DispatcherState.DS_STOPPED, dispatcher.State);
        }
        finally
        {
            await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));
            foreach (var consumer in _unopenedConsumers)
            {
                try
                {
                    consumer.Dispose();
                }
                catch (InvalidOperationException) when (throwOnDispose) { }
            }
        }
    }

    void IObserver<LogEvent>.OnNext(LogEvent value)
    {
        // This event occurs after publication of consumers and before any performer is opened.
        if (value.MessageTemplate.Text != "Dispatcher: Dispatcher starting" || _dispatcher is null)
            return;

        _unopenedConsumers = _dispatcher.Consumers.ToArray();
        foreach (var consumer in _unopenedConsumers)
            consumer.Shut(consumer.Subscription.RoutingKey);
    }

    void IObserver<LogEvent>.OnCompleted() { }
    void IObserver<LogEvent>.OnError(Exception error) { }
}
