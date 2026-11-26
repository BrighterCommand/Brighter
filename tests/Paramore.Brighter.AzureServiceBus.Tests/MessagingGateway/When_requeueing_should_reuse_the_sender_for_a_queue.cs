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
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.AzureServiceBus.Tests.Fakes;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusRequeueSenderReuseTests
{
    [Fact]
    public async Task When_requeueing_twice_to_the_same_queue_should_open_one_sender_and_close_none()
    {
        //Arrange
        var client = NewClient();
        await using var scheduler = new AzureServiceBusRequeueScheduler(client);
        var message = NewMessage();

        //Act
        await scheduler.RequeueAsync(message, new ChannelName("queue"), TimeSpan.Zero);
        await scheduler.RequeueAsync(message, new ChannelName("queue"), TimeSpan.Zero);

        //Assert
        Assert.Equal(1, client.SenderCreationAttempts);
        Assert.Equal(2, client.Scheduled.Count);
        Assert.False(Assert.Single(client.Senders).Disposed);
    }

    [Fact]
    public void When_the_sync_scheduler_requeues_should_reuse_the_sender_it_already_opened()
    {
        //Arrange
        var client = NewClient();
        using var scheduler = new AzureServiceBusRequeueScheduler(client);
        var message = NewMessage();
        scheduler.Requeue(message, new ChannelName("queue"), TimeSpan.Zero);

        //Act
        scheduler.Requeue(message, new ChannelName("queue"), TimeSpan.Zero);

        //Assert
        Assert.Equal(1, client.SenderCreationAttempts);
        Assert.Equal(2, client.Scheduled.Count);
    }

    [Fact]
    public async Task When_a_requeue_fails_should_keep_the_sender_for_the_next_attempt()
    {
        //Arrange
        var client = NewClient();
        await using var scheduler = new AzureServiceBusRequeueScheduler(client);
        var message = NewMessage();
        client.ScheduleFailure = new InvalidOperationException("the broker rejected the schedule");

        //Act
        await Assert.ThrowsAsync<ChannelFailureException>(
            () => scheduler.RequeueAsync(message, new ChannelName("queue"), TimeSpan.Zero));
        client.ScheduleFailure = null;
        await scheduler.RequeueAsync(message, new ChannelName("queue"), TimeSpan.Zero);

        //Assert
        Assert.Equal(1, client.SenderCreationAttempts);
        Assert.Equal(2, client.Scheduled.Count);
        Assert.False(Assert.Single(client.Senders).Disposed);
    }

    [Fact]
    public async Task When_disposing_the_scheduler_should_close_the_sender_it_held()
    {
        //Arrange
        var client = NewClient();
        var scheduler = new AzureServiceBusRequeueScheduler(client);
        await scheduler.RequeueAsync(NewMessage(), new ChannelName("queue"), TimeSpan.Zero);
        var sender = Assert.Single(client.Senders);

        //Act
        await scheduler.DisposeAsync();

        //Assert
        Assert.Equal(1, sender.CloseCount);
    }

    [Fact]
    public void When_disposing_both_ways_should_close_the_sender_once()
    {
        //Arrange
        var client = NewClient();
        var scheduler = new AzureServiceBusRequeueScheduler(client);
        scheduler.Requeue(NewMessage(), new ChannelName("queue"), TimeSpan.Zero);
        var sender = Assert.Single(client.Senders);

        //Act
        scheduler.Dispose();
        scheduler.Dispose();

        //Assert
        Assert.Equal(1, sender.CloseCount);
    }

    [Fact]
    public async Task When_requeueing_after_the_scheduler_is_disposed_should_throw()
    {
        //Arrange
        var client = NewClient();
        var scheduler = new AzureServiceBusRequeueScheduler(client);
        await scheduler.DisposeAsync();

        //Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => scheduler.RequeueAsync(NewMessage(), new ChannelName("queue"), TimeSpan.Zero));
        Assert.Empty(client.Scheduled);
    }

    [Fact]
    public async Task When_a_consumer_owns_the_scheduler_should_close_it_on_dispose()
    {
        //Arrange
        var client = NewClient();
        var scheduler = new AzureServiceBusRequeueScheduler(client);
        await scheduler.RequeueAsync(NewMessage(), new ChannelName("claim-channel"), TimeSpan.Zero);
        var sender = Assert.Single(client.Senders);

        //Act
        await using (var consumer = NewConsumer(scheduler, ownsRequeueScheduler: true))
        {
        }

        //Assert
        Assert.Equal(1, sender.CloseCount);
    }

    [Fact]
    public async Task When_a_consumer_shares_the_scheduler_should_leave_it_open_on_dispose()
    {
        //Arrange
        var client = NewClient();
        await using var scheduler = new AzureServiceBusRequeueScheduler(client);
        await scheduler.RequeueAsync(NewMessage(), new ChannelName("claim-channel"), TimeSpan.Zero);
        var sender = Assert.Single(client.Senders);

        //Act
        await using (var consumer = NewConsumer(scheduler, ownsRequeueScheduler: false))
        {
        }

        //Assert
        Assert.Equal(0, sender.CloseCount);
    }

    private static InMemoryRetryServiceBusClient NewClient()
        => new(ServiceBusModelFactory.ServiceBusReceivedMessage());

    private static Message NewMessage()
        => new(new MessageHeader(Id.Random(), new RoutingKey("claims"), MessageType.MT_COMMAND),
            new MessageBody(Encoding.UTF8.GetBytes("claim checks")));

    private static AzureServiceBusQueueConsumer NewConsumer(IAmAMessageScheduler scheduler, bool ownsRequeueScheduler)
    {
        var subscription = new AzureServiceBusSubscription(
            new SubscriptionName("claim-subscription"), new ChannelName("claim-channel"), new RoutingKey("claims"),
            requestType: typeof(Command),
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Assume,
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration { UseServiceBusQueue = true });
        return new AzureServiceBusQueueConsumer(subscription, scheduler, new FakeAdministrationClient(),
            new FakeServiceBusReceiverProvider(new FakeServiceBusReceiverWrapper()), ownsRequeueScheduler);
    }
}
