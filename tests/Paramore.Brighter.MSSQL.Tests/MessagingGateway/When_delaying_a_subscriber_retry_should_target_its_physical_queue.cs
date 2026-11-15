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

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

public class MsSqlSubscriberRetryTests
{
    private readonly RelationalDatabaseConfiguration _configuration = new("Server=127.0.0.1,1;Database=unused;User Id=unused;Password=unused;Connect Timeout=1;Encrypt=false", queueStoreTable: "queue");
    private readonly TimeSpan _delay = TimeSpan.FromSeconds(3);

    [Test]
    [Arguments(false)]
    [Arguments(true)]
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
        await Assert.That(accepted).IsTrue();
        await Assert.That(scheduler.Destination?.Value).IsEqualTo("subscriber");
        await Assert.That(scheduler.Delay).IsEqualTo(_delay);
        await Assert.That(scheduler.Message?.Id).IsEqualTo(message.Id);
        await Assert.That(scheduler.Message?.Header.Topic.Value).IsEqualTo("logical.topic");
        await Assert.That(message.Header.Topic.Value).IsEqualTo("logical.topic");
        await Assert.That(scheduler.Message?.Body.Value).IsEqualTo("payload");
        if (async)
            await Assert.That(scheduler.Cancellation).IsEqualTo(cancellation.Token);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_targeted_scheduling_fails_should_propagate_the_failure_without_republishing(bool async)
    {
        //Arrange
        var scheduler = new InMemorySubscriberRetryScheduler { FailScheduling = true };
        var factory = new MsSqlMessageConsumerFactory(_configuration, scheduler);
        using var consumer = async ? (MsSqlMessageConsumer)factory.CreateAsync(Subscription()) : (MsSqlMessageConsumer)factory.Create(Subscription());
        var message = Message();

        //Act
        var exception = async
            ? await Assert.That(() => consumer.RequeueAsync(message, _delay)).ThrowsExactly<InvalidOperationException>()
            : (await Assert.That(() => consumer.Requeue(message, _delay)).ThrowsExactly<InvalidOperationException>());

        //Assert
        await Assert.That(exception.Message).IsEqualTo("Scheduling failed");
        await Assert.That(scheduler.Message).IsNull();
        await Assert.That(message.Header.Topic.Value).IsEqualTo("logical.topic");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_a_subscriber_retry_is_already_cancelled_should_not_schedule_or_modify_the_message(bool delayed)
    {
        //Arrange
        var scheduler = new InMemorySubscriberRetryScheduler();
        using var consumer = new MsSqlMessageConsumer(_configuration, "subscriber", true, scheduler: scheduler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var message = Message();

        //Act
        await Assert.That(() => consumer.RequeueAsync(message,
            delayed ? _delay : TimeSpan.Zero, cancellation.Token)).Throws<OperationCanceledException>();

        //Assert
        await Assert.That(scheduler.Message).IsNull();
        await Assert.That(scheduler.AsyncCalls).IsEqualTo(0);
        await Assert.That(message.Header.Bag.ContainsKey(Paramore.Brighter.Message.OriginalMessageIdHeaderName)).IsFalse();
    }

    private MsSqlSubscription Subscription() => new(new("subscription"), new("subscriber"), new("logical.topic"),
        requestType: typeof(Paramore.Brighter.MSSQL.Tests.TestDoubles.MyCommand), requeueDelay: _delay) { IsIndependentSubscriber = true };

    private static Message Message() => new(new MessageHeader(Id.Random(), new RoutingKey("logical.topic"), MessageType.MT_EVENT), new MessageBody("payload"));
}
