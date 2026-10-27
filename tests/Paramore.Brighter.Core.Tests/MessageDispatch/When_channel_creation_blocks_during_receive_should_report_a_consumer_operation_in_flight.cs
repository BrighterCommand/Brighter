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

using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch;

public class DispatcherConsumerOperationInFlightTests
{
    [Fact]
    public async Task When_channel_creation_blocks_during_receive_should_report_a_consumer_operation_in_flight()
    {
        // Arrange
        using var channelFactory = new InMemoryUnopenedConsumerChannelFactory(throwOnDispose: false);
        channelFactory.PauseNextCreation();
        var registry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()),
            new SimpleMessageMapperFactoryAsync(_ => new MyEventMessageMapperAsync()));
        registry.Register<MyEvent, MyEventMessageMapper>();
        registry.RegisterAsync<MyEvent, MyEventMessageMapperAsync>();
        Subscription[] subscriptions =
        [
            new Subscription<MyEvent>(
                new SubscriptionName("alpha"),
                channelName: new ChannelName("alpha-channel"),
                routingKey: new RoutingKey("alpha-topic"),
                channelFactory: channelFactory,
                messagePumpType: MessagePumpType.Reactor,
                noOfPerformers: 1,
                timeOut: TimeSpan.FromMilliseconds(10),
                emptyChannelDelay: TimeSpan.FromMilliseconds(10))
        ];
        using var dispatcher = new Dispatcher(new SpyCommandProcessor(), subscriptions, registry, registry);
        Task? receive = null;

        try
        {
            receive = Task.Run(() => dispatcher.Receive());
            await channelFactory.CreationPaused.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // Act
            var whileBlocked = dispatcher.GetLifecycleSnapshot();
            channelFactory.ResumeCreation();
            await receive.WaitAsync(TimeSpan.FromSeconds(10));
            var afterRelease = dispatcher.GetLifecycleSnapshot();

            // Assert
            Assert.Equal(1, whileBlocked.ConsumerOperationsInFlight);
            Assert.Equal(0, afterRelease.ConsumerOperationsInFlight);
        }
        finally
        {
            channelFactory.ResumeCreation();
            if (receive is not null)
                await receive.WaitAsync(TimeSpan.FromSeconds(10));
            await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));
        }
    }
}
