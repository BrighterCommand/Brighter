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

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusLockRenewalLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_cancellation_callbacks_request_disposal_should_not_hold_lifecycle_gates(bool settleFirst)
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(4)) { BlockRenewal = true };
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            channelName: new ChannelName("locks"), routingKey: new RoutingKey("locks"), makeChannels: OnMissingChannel.Assume);
        await using var consumer = new AzureServiceBusConsumerFactory(client).CreateAsync(subscription);
        Task nestedDisposal = Task.CompletedTask;
        var callbackEnteredDisposal = false;
        receiver.RenewalCancellationCallback = () =>
        {
            var entry = Task.Factory.StartNew(() => { nestedDisposal = consumer.DisposeAsync().AsTask(); },
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            callbackEnteredDisposal = entry.Wait(TimeSpan.FromSeconds(3));
        };
        var message = Assert.Single(await consumer.ReceiveAsync());
        await receiver.RenewalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        if (settleFirst) await consumer.AcknowledgeAsync(message);
        else await consumer.DisposeAsync();
        await nestedDisposal.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(callbackEnteredDisposal, "Cancellation callbacks must be able to enter disposal from another thread.");
        Assert.Equal(0, receiver.RenewalsInFlight);
        Assert.True(receiver.IsClosed);
    }

    [Fact]
    public async Task When_receive_overlaps_last_session_settlement_should_start_a_fresh_renewal()
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(2, TimeSpan.FromSeconds(4));
        var session = new InMemoryLockingServiceBusSessionReceiver(receiver, TimeSpan.FromSeconds(4));
        await using var client = new InMemoryLockingServiceBusClient(session);
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            channelName: new ChannelName("locks"), routingKey: new RoutingKey("locks"),
            bufferSize: 1, makeChannels: OnMissingChannel.Assume,
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration { RequireSession = true });
        await using var consumer = new AzureServiceBusConsumerFactory(client).CreateAsync(subscription);
        var first = Assert.Single(await consumer.ReceiveAsync());
        receiver.BlockReceive = true;
        receiver.BlockCompletion = true;
        var pendingReceive = consumer.ReceiveAsync();
        await receiver.BlockedReceiveStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        try
        {
            var settlement = consumer.AcknowledgeAsync(first);
            await receiver.CompletionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var renewalsBeforeReceive = session.RenewalCount;
            receiver.AllowReceive.TrySetResult(true);
            var second = Assert.Single(await pendingReceive);
            receiver.AllowCompletion.TrySetResult(true);
            await settlement;
            var elapsed = Stopwatch.StartNew();
            while (session.RenewalCount == renewalsBeforeReceive && elapsed.Elapsed < TimeSpan.FromSeconds(5))
                await Task.Delay(20);
            await consumer.AcknowledgeAsync(second);

            // Assert
            Assert.True(session.RenewalCount > renewalsBeforeReceive, "The new batch must not reuse the stopped session renewal.");
            Assert.Equal(2, receiver.CompletedCount);
        }
        finally
        {
            receiver.AllowReceive.TrySetResult(true);
            receiver.AllowCompletion.TrySetResult(true);
        }
    }

    [Theory]
    [InlineData("complete", false)]
    [InlineData("complete", true)]
    [InlineData("abandon", false)]
    [InlineData("abandon", true)]
    [InlineData("reject", false)]
    [InlineData("reject", true)]
    [InlineData("dispose", false)]
    [InlineData("dispose", true)]
    public async Task When_settling_a_message_should_stop_lock_renewal(string operation, bool useAsync)
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(2)) { BlockRenewal = true };
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            channelName: new ChannelName("locks"), routingKey: new RoutingKey("locks"), makeChannels: OnMissingChannel.Assume);
        using var consumer = new AzureServiceBusConsumerFactory(client).Create(subscription);
        var asyncConsumer = (IAmAMessageConsumerAsync)consumer;
        var message = Assert.Single(await asyncConsumer.ReceiveAsync());
        await receiver.RenewalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        if (useAsync)
        {
            switch (operation)
            {
                case "complete": await asyncConsumer.AcknowledgeAsync(message); break;
                case "abandon": await asyncConsumer.NackAsync(message); break;
                case "reject": await asyncConsumer.RejectAsync(message); break;
                case "dispose": await asyncConsumer.DisposeAsync(); break;
            }
        }
        else
        {
            switch (operation)
            {
                case "complete": consumer.Acknowledge(message); break;
                case "abandon": consumer.Nack(message); break;
                case "reject": consumer.Reject(message); break;
                case "dispose": consumer.Dispose(); break;
            }
        }

        // Assert
        Assert.Equal(0, receiver.RenewalsInFlight);
        Assert.Equal(operation == "complete" ? 1 : 0, receiver.CompletedCount);
        Assert.Equal(operation == "abandon" ? 1 : 0, receiver.AbandonedCount);
        Assert.Equal(operation == "reject" ? 1 : 0, receiver.DeadLetteredCount);
        Assert.Equal(operation == "dispose", receiver.IsClosed);
        var attempts = receiver.RenewalAttemptCount;
        await Task.Delay(TimeSpan.FromMilliseconds(1200));
        Assert.Equal(attempts, receiver.RenewalAttemptCount);
    }

    [Fact]
    public async Task When_replacing_a_failed_receiver_should_stop_its_renewals()
    {
        // Arrange
        var firstReceiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(2)) { BlockRenewal = true };
        var replacement = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(2));
        await using var client = new InMemoryLockingServiceBusClient(firstReceiver, replacement);
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            channelName: new ChannelName("locks"), routingKey: new RoutingKey("locks"), makeChannels: OnMissingChannel.Assume);
        await using var consumer = new AzureServiceBusConsumerFactory(client).CreateAsync(subscription);
        await consumer.ReceiveAsync();
        await firstReceiver.RenewalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        firstReceiver.ReceiveException = new InvalidOperationException("The connection failed.");

        // Act
        await Assert.ThrowsAsync<ChannelFailureException>(() => consumer.ReceiveAsync());
        var message = Assert.Single(await consumer.ReceiveAsync());
        await consumer.AcknowledgeAsync(message);

        // Assert
        Assert.True(firstReceiver.IsClosed);
        Assert.Equal(0, firstReceiver.RenewalsInFlight);
        Assert.Equal(1, replacement.CompletedCount);
    }

    [Fact]
    public async Task When_renewal_budget_expires_should_cancel_an_in_flight_renewal()
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(2)) { BlockRenewal = true };
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            channelName: new ChannelName("locks"), routingKey: new RoutingKey("locks"), makeChannels: OnMissingChannel.Assume,
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration
            {
                MaxAutoLockRenewalDuration = TimeSpan.FromMilliseconds(1500)
            });
        await using var consumer = new AzureServiceBusConsumerFactory(client).CreateAsync(subscription);
        await consumer.ReceiveAsync();

        // Act
        await receiver.RenewalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(1, receiver.RenewalAttemptCount);
        Assert.Equal(0, receiver.RenewalsInFlight);
        Assert.False(receiver.IsClosed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_purging_should_stop_renewing_messages_removed_from_the_buffer(bool useAsync)
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(2)) { BlockRenewal = true };
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            channelName: new ChannelName("locks"), routingKey: new RoutingKey("locks"), makeChannels: OnMissingChannel.Assume,
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration { UseServiceBusQueue = true });
        using var consumer = new AzureServiceBusConsumerFactory(client).Create(subscription);
        var asyncConsumer = (IAmAMessageConsumerAsync)consumer;
        await asyncConsumer.ReceiveAsync();
        await receiver.RenewalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        if (useAsync) await asyncConsumer.PurgeAsync();
        else consumer.Purge();

        // Assert
        Assert.True(receiver.IsClosed);
        Assert.Equal(0, receiver.RenewalsInFlight);
    }

    [Fact]
    public async Task When_disposal_overlaps_settlement_should_wait_for_renewal_to_finish()
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(2))
        {
            BlockRenewal = true,
            HoldCancelledRenewal = true
        };
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            channelName: new ChannelName("locks"), routingKey: new RoutingKey("locks"), makeChannels: OnMissingChannel.Assume);
        await using var consumer = new AzureServiceBusConsumerFactory(client).CreateAsync(subscription);
        var message = Assert.Single(await consumer.ReceiveAsync());
        await receiver.RenewalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        var settlement = consumer.AcknowledgeAsync(message);
        bool closedBeforeRenewalFinished;
        bool disposalFinishedEarly;
        try
        {
            await receiver.RenewalCancellationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var disposal = consumer.DisposeAsync().AsTask();
            closedBeforeRenewalFinished = receiver.IsClosed;
            disposalFinishedEarly = disposal.IsCompleted;
            receiver.AllowRenewalToFinish.TrySetResult(true);
            await Task.WhenAll(settlement, disposal).WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            receiver.AllowRenewalToFinish.TrySetResult(true);
        }

        // Assert
        Assert.False(closedBeforeRenewalFinished);
        Assert.False(disposalFinishedEarly);
        Assert.Equal(0, receiver.RenewalsInFlight);
        Assert.True(receiver.IsClosed);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_abandoning_or_rejecting_a_session_message_should_preserve_the_remaining_batch(
        bool reject, bool useAsync)
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(2, TimeSpan.FromMinutes(1));
        var sessionReceiver = new InMemoryLockingServiceBusSessionReceiver(receiver, TimeSpan.FromMinutes(1));
        await using var client = new InMemoryLockingServiceBusClient(sessionReceiver);
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            channelName: new ChannelName("locks"), routingKey: new RoutingKey("locks"),
            bufferSize: 2, makeChannels: OnMissingChannel.Assume,
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration { RequireSession = true });
        using var consumer = new AzureServiceBusConsumerFactory(client).Create(subscription);
        var asyncConsumer = (IAmAMessageConsumerAsync)consumer;
        var messages = await asyncConsumer.ReceiveAsync();
        Assert.Equal(2, messages.Length);

        // Act
        if (useAsync)
        {
            if (reject) await asyncConsumer.RejectAsync(messages[0]);
            else await asyncConsumer.NackAsync(messages[0]);
        }
        else
        {
            if (reject) consumer.Reject(messages[0]);
            else consumer.Nack(messages[0]);
        }
        var closedBeforeLastMessage = receiver.IsClosed;
        await asyncConsumer.AcknowledgeAsync(messages[1]);

        // Assert
        Assert.False(closedBeforeLastMessage);
        Assert.Equal(1, receiver.CompletedCount);
        Assert.Equal(reject ? 0 : 1, receiver.AbandonedCount);
        Assert.Equal(reject ? 1 : 0, receiver.DeadLetteredCount);
        Assert.True(receiver.IsClosed);
    }
}
