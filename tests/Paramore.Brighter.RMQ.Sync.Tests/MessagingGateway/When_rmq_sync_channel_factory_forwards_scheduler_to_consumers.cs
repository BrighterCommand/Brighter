#region Licence
/* The MIT License (MIT)

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

using System;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway;

public class When_rmq_sync_channel_factory_forwards_scheduler_to_consumers
{
    private readonly RmqMessagingGatewayConnection _connection = new()
    {
        AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672")),
        Exchange = new Exchange("test.exchange")
    };

    [Fact]
    public void Should_forward_scheduler_to_consumer_factory()
    {
        // Arrange
        var consumerFactory = new RmqMessageConsumerFactory(_connection);
        var channelFactory = new ChannelFactory(consumerFactory);
        var scheduler = new StubMessageScheduler();

        // Act — set scheduler on the channel factory
        ((IAmAChannelFactoryWithScheduler)channelFactory).Scheduler = scheduler;

        // Assert — the consumer factory received the scheduler
        Assert.Same(scheduler, consumerFactory.Scheduler);
    }

    [Fact]
    public void Should_read_scheduler_from_consumer_factory()
    {
        // Arrange — consumer factory has a scheduler from construction
        var scheduler = new StubMessageScheduler();
        var consumerFactory = new RmqMessageConsumerFactory(_connection, scheduler);
        var channelFactory = new ChannelFactory(consumerFactory);

        // Assert — channel factory reads from the consumer factory
        Assert.Same(scheduler, ((IAmAChannelFactoryWithScheduler)channelFactory).Scheduler);
    }

    private class StubMessageScheduler : IAmAMessageScheduler;
}
