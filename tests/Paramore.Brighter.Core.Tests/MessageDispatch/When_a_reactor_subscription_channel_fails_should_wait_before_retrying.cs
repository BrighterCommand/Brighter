#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.Testing;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch;

public class ReactorSubscriptionChannelFailureDelayTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData(1500, false)]
    [InlineData(null, true)]
    [InlineData(1500, true)]
    public async Task When_a_reactor_subscription_channel_fails_should_wait_before_retrying(
        int? delayMilliseconds, bool brokenCircuit)
    {
        //Arrange
        var channelFactory = new InMemoryRetryTimingChannelFactory(brokenCircuit);
        var subscription = new Subscription<MyEvent>(
            new SubscriptionName("retry-delay"),
            channelName: new ChannelName("retry-delay-channel"),
            routingKey: new RoutingKey("retry-delay-topic"),
            channelFactory: channelFactory,
            messagePumpType: MessagePumpType.Reactor,
            channelFailureDelay: delayMilliseconds.HasValue
                ? TimeSpan.FromMilliseconds(delayMilliseconds.Value)
                : null);
        using var registry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyEventMessageMapper()), null);
        registry.Register<MyEvent, MyEventMessageMapper>();
        await using var dispatcher = new Dispatcher(new SpyCommandProcessor(), [subscription], registry);

        //Act
        dispatcher.Receive();
        var retryDelays = await channelFactory.RetryDelays.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await dispatcher.End().WaitAsync(TimeSpan.FromSeconds(10));

        //Assert
        Assert.Equal(2, retryDelays.Length);
        Assert.All(retryDelays, actualDelay =>
            Assert.True(actualDelay >= subscription.ChannelFailureDelay - TimeSpan.FromMilliseconds(20),
                $"Expected at least {subscription.ChannelFailureDelay.TotalMilliseconds} ms between attempts, " +
                $"allowing 20 ms timer tolerance, but observed {actualDelay.TotalMilliseconds} ms."));
    }
}
