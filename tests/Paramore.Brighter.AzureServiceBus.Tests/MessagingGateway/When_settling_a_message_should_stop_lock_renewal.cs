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
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusLockRenewalLifetimeTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_cancellation_callbacks_request_disposal_should_not_hold_lifecycle_gates(bool settleFirst)
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(4))
        {
            BlockRenewal = true,
            BlockCompletion = settleFirst
        };
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
        var message = await Assert.That(await consumer.ReceiveAsync()).HasSingleItem();
        await receiver.RenewalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        if (settleFirst)
        {
            var settlement = consumer.AcknowledgeAsync(message);
            try
            {
                await nestedDisposal.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                receiver.AllowCompletion.TrySetResult(true);
            }
            var exception = await Assert.That(() => settlement.WaitAsync(TimeSpan.FromSeconds(5))).ThrowsExactly<ServiceBusException>();
            await Assert.That(exception.Reason).IsEqualTo(ServiceBusFailureReason.MessageLockLost);
        }
        else await consumer.DisposeAsync();
        await nestedDisposal.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await Assert.That(callbackEnteredDisposal).IsTrue().Because("Cancellation callbacks must be able to enter disposal from another thread.");
        await Assert.That(receiver.RenewalsInFlight).IsEqualTo(0);
        await Assert.That(receiver.IsClosed).IsTrue();
    }

    [Test]
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
        var first = await Assert.That(await consumer.ReceiveAsync()).HasSingleItem();
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
            var second = await Assert.That(await pendingReceive).HasSingleItem();
            receiver.AllowCompletion.TrySetResult(true);
            await settlement;
            var elapsed = Stopwatch.StartNew();
            while (session.RenewalCount == renewalsBeforeReceive && elapsed.Elapsed < TimeSpan.FromSeconds(5))
                await Task.Delay(20);
            await consumer.AcknowledgeAsync(second);

            // Assert
            await Assert.That(session.RenewalCount > renewalsBeforeReceive).IsTrue().Because("The new batch must not reuse the stopped session renewal.");
            await Assert.That(receiver.CompletedCount).IsEqualTo(2);
        }
        finally
        {
            receiver.AllowReceive.TrySetResult(true);
            receiver.AllowCompletion.TrySetResult(true);
        }
    }

    [Test]
    [Arguments("complete", false)]
    [Arguments("complete", true)]
    [Arguments("abandon", false)]
    [Arguments("abandon", true)]
    [Arguments("reject", false)]
    [Arguments("reject", true)]
    [Arguments("dispose", false)]
    [Arguments("dispose", true)]
    public async Task When_settling_a_message_should_stop_lock_renewal(string operation, bool useAsync)
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(2)) { BlockRenewal = true };
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            channelName: new ChannelName("locks"), routingKey: new RoutingKey("locks"), makeChannels: OnMissingChannel.Assume);
        using var consumer = new AzureServiceBusConsumerFactory(client).Create(subscription);
        var asyncConsumer = (IAmAMessageConsumerAsync)consumer;
        var message = await Assert.That(await asyncConsumer.ReceiveAsync()).HasSingleItem();
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
        await Assert.That(receiver.RenewalsInFlight).IsEqualTo(0);
        await Assert.That(receiver.CompletedCount).IsEqualTo(operation == "complete" ? 1 : 0);
        await Assert.That(receiver.AbandonedCount).IsEqualTo(operation == "abandon" ? 1 : 0);
        await Assert.That(receiver.DeadLetteredCount).IsEqualTo(operation == "reject" ? 1 : 0);
        await Assert.That(receiver.IsClosed).IsEqualTo(operation == "dispose");
        var attempts = receiver.RenewalAttemptCount;
        await Task.Delay(TimeSpan.FromMilliseconds(1200));
        await Assert.That(receiver.RenewalAttemptCount).IsEqualTo(attempts);
    }

    [Test]
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
        await Assert.That(() => consumer.ReceiveAsync()).ThrowsExactly<ChannelFailureException>();
        var message = await Assert.That(await consumer.ReceiveAsync()).HasSingleItem();
        await consumer.AcknowledgeAsync(message);

        // Assert
        await Assert.That(firstReceiver.IsClosed).IsTrue();
        await Assert.That(firstReceiver.RenewalsInFlight).IsEqualTo(0);
        await Assert.That(replacement.CompletedCount).IsEqualTo(1);
    }

    [Test]
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
        await Assert.That(receiver.RenewalAttemptCount).IsEqualTo(1);
        await Assert.That(receiver.RenewalsInFlight).IsEqualTo(0);
        await Assert.That(receiver.IsClosed).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
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
        await Assert.That(receiver.IsClosed).IsTrue();
        await Assert.That(receiver.RenewalsInFlight).IsEqualTo(0);
    }

    [Test]
    public async Task When_disposal_overlaps_settlement_should_wait_for_renewal_to_finish()
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(2))
        {
            BlockRenewal = true,
            HoldCancelledRenewal = true,
            BlockCompletion = true
        };
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            channelName: new ChannelName("locks"), routingKey: new RoutingKey("locks"), makeChannels: OnMissingChannel.Assume);
        await using var consumer = new AzureServiceBusConsumerFactory(client).CreateAsync(subscription);
        var message = await Assert.That(await consumer.ReceiveAsync()).HasSingleItem();
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
            await disposal.WaitAsync(TimeSpan.FromSeconds(5));
            receiver.AllowCompletion.TrySetResult(true);
            var exception = await Assert.That(() => settlement.WaitAsync(TimeSpan.FromSeconds(5))).ThrowsExactly<ServiceBusException>();
            await Assert.That(exception.Reason).IsEqualTo(ServiceBusFailureReason.MessageLockLost);
        }
        finally
        {
            receiver.AllowRenewalToFinish.TrySetResult(true);
            receiver.AllowCompletion.TrySetResult(true);
        }

        // Assert
        await Assert.That(closedBeforeRenewalFinished).IsFalse();
        await Assert.That(disposalFinishedEarly).IsFalse();
        await Assert.That(receiver.RenewalsInFlight).IsEqualTo(0);
        await Assert.That(receiver.IsClosed).IsTrue();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
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
        await Assert.That(messages.Length).IsEqualTo(2);

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
        await Assert.That(closedBeforeLastMessage).IsFalse();
        await Assert.That(receiver.CompletedCount).IsEqualTo(1);
        await Assert.That(receiver.AbandonedCount).IsEqualTo(reject ? 0 : 1);
        await Assert.That(receiver.DeadLetteredCount).IsEqualTo(reject ? 1 : 0);
        await Assert.That(receiver.IsClosed).IsTrue();
    }
}
