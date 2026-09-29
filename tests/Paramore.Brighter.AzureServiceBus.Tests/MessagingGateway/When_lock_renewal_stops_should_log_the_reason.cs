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
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusLockLoggingTests
{
    private static readonly InMemoryServiceBusLogProvider s_logs = new();

    static AzureServiceBusLockLoggingTests() => ApplicationLogging.LoggerFactory.AddProvider(s_logs);

    [Test]
    public async Task When_transient_failures_continue_until_lock_loss_should_warn_once_and_log_retries_at_debug()
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(2))
        {
            RenewalFailuresRemaining = int.MaxValue,
            RenewalFailureIsTransient = true
        };
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        await using var consumer = new AzureServiceBusConsumerFactory(client).CreateAsync(CreateSubscription(TimeSpan.FromSeconds(30)));
        var message = await Assert.That(await consumer.ReceiveAsync()).HasSingleItem();

        // Act
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(5) && !s_logs.Entries.Any(entry =>
            entry.Message.Contains("stopped after a failure") &&
            entry.Properties.TryGetValue("LockId", out var id) && Equals(id, message.Id.Value)))
            await Task.Delay(20);
        await consumer.DisposeAsync();

        // Assert
        var logs = s_logs.Entries.Where(entry => entry.Properties.TryGetValue("LockId", out var id) && Equals(id, message.Id.Value)).ToArray();
        await Assert.That(System.Linq.Enumerable.Where(logs, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("Transient failure"))).HasSingleItem();
        await Assert.That(System.Linq.Enumerable.Any(logs, entry => entry.Level == LogLevel.Debug && entry.Message.Contains("Transient failure"))).IsTrue();
        await Assert.That(System.Linq.Enumerable.Where(logs, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("stopped after a failure"))).HasSingleItem();
        await Assert.That(receiver.RenewalAttemptCount >= 3).IsTrue();
        await Assert.That(receiver.RenewalCount).IsEqualTo(0);
    }

    [Test]
    public async Task When_renewal_stops_without_broker_lock_loss_should_preserve_dispatch_until_the_deadline()
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(2, TimeSpan.FromSeconds(4))
        {
            RenewalException = new InvalidOperationException("The transport cannot renew.")
        };
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(client));
        await using var channel = factory.CreateAsyncChannel(CreateSubscription(TimeSpan.FromSeconds(30)));
        var first = await channel.ReceiveAsync(null);
        await channel.AcknowledgeAsync(first);

        // Act
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(3) && !s_logs.Entries.Any(entry =>
            entry.Message.Contains("stopped after a failure") &&
            entry.Properties.TryGetValue("EntityPath", out var entity) && Equals(entity, receiver.EntityPath)))
            await Task.Delay(20);
        var second = await channel.ReceiveAsync(null);
        if (!second.IsEmpty) await channel.AcknowledgeAsync(second);

        // Assert
        await Assert.That(second.IsEmpty).IsFalse();
        await Assert.That(receiver.CompletedCount).IsEqualTo(2);
        await AssertFailureWarning(second.Id.Value, receiver.EntityPath, transient: false);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_renewal_fails_should_log_the_lock_identity(bool transient)
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(2))
        {
            RenewalFailuresRemaining = 1,
            RenewalFailureIsTransient = transient
        };
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        await using var consumer = new AzureServiceBusConsumerFactory(client).CreateAsync(CreateSubscription(TimeSpan.FromSeconds(30)));
        var message = await Assert.That(await consumer.ReceiveAsync()).HasSingleItem();

        // Act
        await WaitForFailureLogAsync(message.Id.Value);
        await consumer.DisposeAsync();

        // Assert
        await AssertFailureWarning(message.Id.Value, receiver.EntityPath, transient);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_the_reported_deadline_has_passed_should_let_the_broker_decide_whether_to_renew(bool brokerRejects)
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(2))
        {
            FirstRenewalReportedLockDuration = TimeSpan.FromSeconds(-1),
            RenewalExceptionAfterFirstSuccess = brokerRejects
                ? new ServiceBusException("The broker has lost the lock.", ServiceBusFailureReason.MessageLockLost)
                : null
        };
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        await using var consumer = new AzureServiceBusConsumerFactory(client).CreateAsync(CreateSubscription(TimeSpan.FromSeconds(30)));
        var message = await Assert.That(await consumer.ReceiveAsync()).HasSingleItem();

        // Act
        if (brokerRejects)
            await WaitForFailureLogAsync(message.Id.Value);
        else
        {
            var elapsed = Stopwatch.StartNew();
            while (receiver.RenewalCount < 2 && elapsed.Elapsed < TimeSpan.FromSeconds(5))
                await Task.Delay(20);
            await consumer.AcknowledgeAsync(message);
        }
        await consumer.DisposeAsync();

        // Assert
        await Assert.That(receiver.RenewalAttemptCount >= 2).IsTrue().Because("A local deadline must not prevent asking the broker to renew.");
        if (brokerRejects)
            await AssertFailureWarning(message.Id.Value, receiver.EntityPath, transient: false);
        else
        {
            await Assert.That(receiver.RenewalCount >= 2).IsTrue();
            await Assert.That(receiver.CompletedCount).IsEqualTo(1);
        }
    }

    [Test]
    public async Task When_renewal_is_cancelled_unexpectedly_should_log_failure_instead_of_budget_exhaustion()
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(2))
        {
            RenewalException = new OperationCanceledException("The transport cancelled the operation.")
        };
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        await using var consumer = new AzureServiceBusConsumerFactory(client).CreateAsync(CreateSubscription(TimeSpan.FromSeconds(30)));
        var message = await Assert.That(await consumer.ReceiveAsync()).HasSingleItem();

        // Act
        await WaitForFailureLogAsync(message.Id.Value);
        await consumer.DisposeAsync();

        // Assert
        await AssertFailureWarning(message.Id.Value, receiver.EntityPath, transient: false);
        await Assert.That(System.Linq.Enumerable.Any(s_logs.Entries, entry => entry.Message.Contains("has been reached") &&
            entry.Properties.TryGetValue("LockId", out var value) && Equals(value, message.Id.Value))).IsFalse();
        await Assert.That(receiver.RenewalAttemptCount).IsEqualTo(1);
    }

    private static async Task WaitForFailureLogAsync(string lockId)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(5) && !s_logs.Entries.Any(entry =>
            entry.Message.Contains("failure", StringComparison.OrdinalIgnoreCase) &&
            entry.Properties.TryGetValue("LockId", out var value) && Equals(value, lockId)))
            await Task.Delay(20);
    }

    private static async Task AssertFailureWarning(string lockId, string entityPath, bool transient)
    {
        var warning = await Assert.That(System.Linq.Enumerable.Where(s_logs.Entries, entry =>
            entry.Message.Contains("failure", StringComparison.OrdinalIgnoreCase) &&
            entry.Properties.TryGetValue("LockId", out var value) && Equals(value, lockId))).HasSingleItem();
        await Assert.That(warning.Level).IsEqualTo(LogLevel.Warning);
        await Assert.That(warning.Properties["LockType"]).IsEqualTo("message");
        await Assert.That(warning.Properties["EntityPath"]).IsEqualTo(entityPath);
        await Assert.That(warning.Properties["LockedUntil"]).IsTypeOf<DateTimeOffset>();
        await Assert.That(warning.Properties["MaxAutoLockRenewalDuration"]).IsEqualTo(TimeSpan.FromSeconds(30));
        await Assert.That(warning.Message).Contains(transient ? "Transient failure" : "stopped after a failure");
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task When_an_expired_buffered_message_is_skipped_should_log_its_identity(bool useAsync, bool useQueue)
    {
        // Arrange
        var topic = Guid.NewGuid().ToString();
        var channelName = Guid.NewGuid().ToString();
        var receiver = new InMemoryLockingServiceBusReceiver(2, TimeSpan.FromSeconds(2));
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            channelName: new ChannelName(channelName), routingKey: new RoutingKey(topic),
            bufferSize: 2, makeChannels: OnMissingChannel.Assume,
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration
            {
                MaxAutoLockRenewalDuration = TimeSpan.Zero,
                UseServiceBusQueue = useQueue
            });
        var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(client));
        Message first;
        Message next;

        // Act
        if (useAsync)
        {
            await using var channel = factory.CreateAsyncChannel(subscription);
            first = await channel.ReceiveAsync(null);
            await channel.AcknowledgeAsync(first);
            await Task.Delay(TimeSpan.FromMilliseconds(2300));
            next = await channel.ReceiveAsync(null);
        }
        else
        {
            using var channel = factory.CreateSyncChannel(subscription);
            first = channel.Receive(null);
            channel.Acknowledge(first);
            await Task.Delay(TimeSpan.FromMilliseconds(2300));
            next = channel.Receive(null);
        }

        // Assert
        await Assert.That(next.IsEmpty).IsTrue();
        var warning = await Assert.That(System.Linq.Enumerable.Where(s_logs.Entries, entry =>
            entry.Level == LogLevel.Warning && entry.Message.Contains("Skipping") &&
            entry.Properties.TryGetValue("Topic", out var value) && Equals(value, topic))).HasSingleItem();
        await Assert.That(warning.Message).Contains("expired or been lost");
        await Assert.That(warning.Properties["ChannelName"]).IsEqualTo(channelName);
        await Assert.That(Guid.TryParse(warning.Properties["Id"]?.ToString(), out _)).IsTrue();
        await Assert.That(warning.Properties["Id"]).IsNotEqualTo(first.Id.Value);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_the_budget_prevents_further_renewal_should_log_once_with_message_context(bool blockRenewal)
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(2)) { BlockRenewal = blockRenewal };
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        var duration = TimeSpan.FromMilliseconds(1500);
        var subscription = CreateSubscription(duration);
        await using var consumer = new AzureServiceBusConsumerFactory(client).CreateAsync(subscription);
        var message = await Assert.That(await consumer.ReceiveAsync()).HasSingleItem();

        // Act
        await WaitForBudgetLogAsync(message.Id.Value);
        await consumer.DisposeAsync();

        // Assert
        await AssertBudgetWarning(message.Id.Value, "message", receiver.EntityPath, duration);
        await Assert.That(receiver.RenewalAttemptCount).IsEqualTo(1);
        await Assert.That(receiver.RenewalsInFlight).IsEqualTo(0);
    }

    [Test]
    public async Task When_a_session_budget_prevents_renewal_should_log_once_for_the_session()
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(2, TimeSpan.FromSeconds(5));
        var session = new InMemoryLockingServiceBusSessionReceiver(receiver, TimeSpan.FromSeconds(5));
        await using var client = new InMemoryLockingServiceBusClient(session);
        var duration = TimeSpan.FromMilliseconds(100);
        var subscription = CreateSubscription(duration, requireSession: true);
        await using var consumer = new AzureServiceBusConsumerFactory(client).CreateAsync(subscription);

        // Act
        await Assert.That((await consumer.ReceiveAsync()).Length).IsEqualTo(2);
        await WaitForBudgetLogAsync(session.SessionId);
        await consumer.DisposeAsync();

        // Assert
        await AssertBudgetWarning(session.SessionId, "session", session.EntityPath, duration);
        await Assert.That(session.RenewalCount).IsEqualTo(0);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_settling_before_a_short_renewal_budget_expires_should_not_warn(bool dispose)
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(5));
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        var subscription = CreateSubscription(TimeSpan.FromMilliseconds(500));
        await using var consumer = new AzureServiceBusConsumerFactory(client).CreateAsync(subscription);
        var message = await Assert.That(await consumer.ReceiveAsync()).HasSingleItem();

        // Act
        await Task.Delay(100);
        if (dispose) await consumer.DisposeAsync();
        else await consumer.AcknowledgeAsync(message);
        await Task.Delay(500);

        // Assert
        await Assert.That(System.Linq.Enumerable.Any(s_logs.Entries, entry => entry.Message.Contains("has been reached") &&
            entry.Properties.TryGetValue("LockId", out var value) && Equals(value, message.Id.Value))).IsFalse();
        await Assert.That(receiver.RenewalAttemptCount).IsEqualTo(0);
        await Assert.That(receiver.CompletedCount).IsEqualTo(dispose ? 0 : 1);
    }

    [Test]
    [Arguments("complete")]
    [Arguments("dispose")]
    [Arguments("disabled")]
    public async Task When_renewal_is_disabled_or_stopped_normally_should_not_log_budget_exhaustion(string operation)
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(2)) { BlockRenewal = true };
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        var subscription = CreateSubscription(operation == "disabled" ? TimeSpan.Zero : TimeSpan.FromMilliseconds(1500));
        await using var consumer = new AzureServiceBusConsumerFactory(client).CreateAsync(subscription);
        var message = await Assert.That(await consumer.ReceiveAsync()).HasSingleItem();
        if (operation != "disabled") await receiver.RenewalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        if (operation == "dispose") await consumer.DisposeAsync();
        else await consumer.AcknowledgeAsync(message);
        await Task.Delay(TimeSpan.FromMilliseconds(700));

        // Assert
        await Assert.That(System.Linq.Enumerable.Any(s_logs.Entries, entry => entry.Message.Contains("has been reached") &&
            entry.Properties.TryGetValue("LockId", out var value) && Equals(value, message.Id.Value))).IsFalse();
        await Assert.That(receiver.RenewalsInFlight).IsEqualTo(0);
    }

    private static AzureServiceBusSubscription<ASBTestCommand> CreateSubscription(TimeSpan duration, bool requireSession = false)
        => new(channelName: new ChannelName("locks"), routingKey: new RoutingKey("locks"),
            bufferSize: 2, makeChannels: OnMissingChannel.Assume,
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration
            {
                MaxAutoLockRenewalDuration = duration,
                RequireSession = requireSession
            });

    private static async Task WaitForBudgetLogAsync(string lockId)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(3) && !s_logs.Entries.Any(entry =>
            entry.Message.Contains("has been reached") &&
            entry.Properties.TryGetValue("LockId", out var value) && Equals(value, lockId)))
            await Task.Delay(20);
    }

    private static async Task AssertBudgetWarning(string lockId, string lockType, string entityPath, TimeSpan duration)
    {
        var warning = await Assert.That(System.Linq.Enumerable.Where(s_logs.Entries, entry =>
            entry.Message.Contains("has been reached") &&
            entry.Properties.TryGetValue("LockId", out var value) && Equals(value, lockId))).HasSingleItem();
        await Assert.That(warning.Level).IsEqualTo(LogLevel.Warning);
        await Assert.That(warning.Properties["LockType"]).IsEqualTo(lockType);
        await Assert.That(warning.Properties["EntityPath"]).IsEqualTo(entityPath);
        await Assert.That(warning.Properties["MaxAutoLockRenewalDuration"]).IsEqualTo(duration);
        await Assert.That(warning.Properties["LockedUntil"]).IsTypeOf<DateTimeOffset>();
        await Assert.That(warning.Message).Contains("processing is not cancelled");
    }
}
