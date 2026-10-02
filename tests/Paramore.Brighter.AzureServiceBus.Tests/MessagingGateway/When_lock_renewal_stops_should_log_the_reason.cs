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
using Microsoft.Extensions.Logging;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusLockLoggingTests
{
    private static readonly InMemoryServiceBusLogProvider s_logs = new();

    static AzureServiceBusLockLoggingTests() => ApplicationLogging.LoggerFactory.AddProvider(s_logs);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
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
        Assert.True(next.IsEmpty);
        var warning = Assert.Single(s_logs.Entries, entry =>
            entry.Level == LogLevel.Warning && entry.Message.Contains("Skipping") &&
            entry.Properties.TryGetValue("Topic", out var value) && Equals(value, topic));
        Assert.Contains("expired or been lost", warning.Message);
        Assert.Equal(channelName, warning.Properties["ChannelName"]);
        Assert.True(Guid.TryParse(warning.Properties["Id"]?.ToString(), out _));
        Assert.NotEqual(first.Id.Value, warning.Properties["Id"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_the_budget_prevents_further_renewal_should_log_once_with_message_context(bool blockRenewal)
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(2)) { BlockRenewal = blockRenewal };
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        var duration = TimeSpan.FromMilliseconds(1500);
        var subscription = CreateSubscription(duration);
        await using var consumer = new AzureServiceBusConsumerFactory(client).CreateAsync(subscription);
        var message = Assert.Single(await consumer.ReceiveAsync());

        // Act
        await WaitForBudgetLogAsync(message.Id.Value);
        await consumer.DisposeAsync();

        // Assert
        AssertBudgetWarning(message.Id.Value, "message", receiver.EntityPath, duration);
        Assert.Equal(1, receiver.RenewalAttemptCount);
        Assert.Equal(0, receiver.RenewalsInFlight);
    }

    [Fact]
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
        Assert.Equal(2, (await consumer.ReceiveAsync()).Length);
        await WaitForBudgetLogAsync(session.SessionId);
        await consumer.DisposeAsync();

        // Assert
        AssertBudgetWarning(session.SessionId, "session", session.EntityPath, duration);
        Assert.Equal(0, session.RenewalCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_settling_before_a_short_renewal_budget_expires_should_not_warn(bool dispose)
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(5));
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        var subscription = CreateSubscription(TimeSpan.FromMilliseconds(500));
        await using var consumer = new AzureServiceBusConsumerFactory(client).CreateAsync(subscription);
        var message = Assert.Single(await consumer.ReceiveAsync());

        // Act
        await Task.Delay(100);
        if (dispose) await consumer.DisposeAsync();
        else await consumer.AcknowledgeAsync(message);
        await Task.Delay(500);

        // Assert
        Assert.DoesNotContain(s_logs.Entries, entry => entry.Properties.ContainsKey("MaxAutoLockRenewalDuration") &&
            entry.Properties.TryGetValue("LockId", out var value) && Equals(value, message.Id.Value));
        Assert.Equal(0, receiver.RenewalAttemptCount);
        Assert.Equal(dispose ? 0 : 1, receiver.CompletedCount);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("dispose")]
    [InlineData("disabled")]
    public async Task When_renewal_is_disabled_or_stopped_normally_should_not_log_budget_exhaustion(string operation)
    {
        // Arrange
        var receiver = new InMemoryLockingServiceBusReceiver(1, TimeSpan.FromSeconds(2)) { BlockRenewal = true };
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        var subscription = CreateSubscription(operation == "disabled" ? TimeSpan.Zero : TimeSpan.FromMilliseconds(1500));
        await using var consumer = new AzureServiceBusConsumerFactory(client).CreateAsync(subscription);
        var message = Assert.Single(await consumer.ReceiveAsync());
        if (operation != "disabled") await receiver.RenewalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        if (operation == "dispose") await consumer.DisposeAsync();
        else await consumer.AcknowledgeAsync(message);
        await Task.Delay(TimeSpan.FromMilliseconds(700));

        // Assert
        Assert.DoesNotContain(s_logs.Entries, entry => entry.Properties.ContainsKey("MaxAutoLockRenewalDuration") &&
            entry.Properties.TryGetValue("LockId", out var value) && Equals(value, message.Id.Value));
        Assert.Equal(0, receiver.RenewalsInFlight);
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
            entry.Properties.ContainsKey("MaxAutoLockRenewalDuration") &&
            entry.Properties.TryGetValue("LockId", out var value) && Equals(value, lockId)))
            await Task.Delay(20);
    }

    private static void AssertBudgetWarning(string lockId, string lockType, string entityPath, TimeSpan duration)
    {
        var warning = Assert.Single(s_logs.Entries, entry =>
            entry.Properties.ContainsKey("MaxAutoLockRenewalDuration") &&
            entry.Properties.TryGetValue("LockId", out var value) && Equals(value, lockId));
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Equal(lockType, warning.Properties["LockType"]);
        Assert.Equal(entityPath, warning.Properties["EntityPath"]);
        Assert.Equal(duration, warning.Properties["MaxAutoLockRenewalDuration"]);
        Assert.IsType<DateTimeOffset>(warning.Properties["LockedUntil"]);
        Assert.Contains("processing is not cancelled", warning.Message);
    }
}
