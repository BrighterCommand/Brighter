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
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.MsSql;
using Paramore.Brighter.MSSQL.Tests.MessagingGateway.TestDoubles;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

public class MsSqlSubscriberRetryTests
{
    private readonly RelationalDatabaseConfiguration _configuration = new("Server=127.0.0.1,1;Database=unused;User Id=unused;Password=unused;Connect Timeout=1;Encrypt=false", queueStoreTable: "queue");
    private readonly TimeSpan _delay = TimeSpan.FromSeconds(3);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_delaying_a_subscriber_retry_should_target_its_physical_queue(bool async)
    {
        //Arrange
        var scheduler = new InMemorySubscriberRetryScheduler();
        var factory = new MsSqlMessageConsumerFactory(_configuration, scheduler);
        var subscription = Subscription();
        var message = Message();
        using var cancellation = new CancellationTokenSource();
        using var consumer = async ? (MsSqlMessageConsumer)factory.CreateAsync(subscription) : (MsSqlMessageConsumer)factory.Create(subscription);

        //Act
        var accepted = async
            ? await consumer.RequeueAsync(message, _delay, cancellation.Token)
            : consumer.Requeue(message, _delay);

        //Assert
        Assert.True(accepted);
        Assert.Equal("subscriber", scheduler.Destination?.Value);
        Assert.Equal(_delay, scheduler.Delay);
        Assert.Equal(message.Id, scheduler.Message?.Id);
        Assert.Equal("logical.topic", scheduler.Message?.Header.Topic.Value);
        Assert.Equal("logical.topic", message.Header.Topic.Value);
        Assert.Equal("payload", scheduler.Message?.Body.Value);
        if (async)
            Assert.Equal(cancellation.Token, scheduler.Cancellation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_targeted_scheduling_fails_should_propagate_the_failure_without_republishing(bool async)
    {
        //Arrange
        var scheduler = new InMemorySubscriberRetryScheduler { FailScheduling = true };
        var factory = new MsSqlMessageConsumerFactory(_configuration, scheduler);
        using var consumer = async ? (MsSqlMessageConsumer)factory.CreateAsync(Subscription()) : (MsSqlMessageConsumer)factory.Create(Subscription());
        var message = Message();

        //Act
        var exception = async
            ? await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.RequeueAsync(message, _delay))
            : Assert.Throws<InvalidOperationException>(() => consumer.Requeue(message, _delay));

        //Assert
        Assert.Equal("Scheduling failed", exception.Message);
        Assert.Null(scheduler.Message);
        Assert.Equal("logical.topic", message.Header.Topic.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_a_subscriber_retry_is_already_cancelled_should_not_schedule_or_modify_the_message(bool delayed)
    {
        //Arrange
        var scheduler = new InMemorySubscriberRetryScheduler();
        using var consumer = new MsSqlMessageConsumer(_configuration, "subscriber", true, scheduler: scheduler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var message = Message();

        //Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => consumer.RequeueAsync(message,
            delayed ? _delay : TimeSpan.Zero, cancellation.Token));

        //Assert
        Assert.Null(scheduler.Message);
        Assert.Equal(0, scheduler.AsyncCalls);
        Assert.False(message.Header.Bag.ContainsKey(Paramore.Brighter.Message.OriginalMessageIdHeaderName));
    }

    private MsSqlSubscription Subscription() => new(new("subscription"), new("subscriber"), new("logical.topic"),
        requestType: typeof(Paramore.Brighter.MSSQL.Tests.TestDoubles.MyCommand), requeueDelay: _delay) { IsIndependentSubscriber = true };

    private static Message Message() => new(new MessageHeader(Id.Random(), new RoutingKey("logical.topic"), MessageType.MT_EVENT), new MessageBody("payload"));
}
