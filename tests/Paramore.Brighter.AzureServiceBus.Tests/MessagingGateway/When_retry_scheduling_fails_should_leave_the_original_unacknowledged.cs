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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessageScheduler.Azure;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusScheduledRetryFailureTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task When_retry_scheduling_fails_should_leave_the_original_unacknowledged(bool synchronous, bool cancel)
    {
        // Arrange
        var (client, factory, subscription) = Create(synchronous);
        using var syncChannel = synchronous ? factory.CreateSyncChannel(subscription) : null;
        await using var asyncChannel = synchronous ? null : await factory.CreateAsyncChannelAsync(subscription);
        var message = synchronous ? syncChannel!.Receive(TimeSpan.FromSeconds(1))
            : await asyncChannel!.ReceiveAsync(TimeSpan.FromSeconds(1));
        message.Header.UpdateHandledCount();
        var originalBag = message.Header.Bag.ToArray();
        var failure = new ServiceBusException("Scheduling failed", ServiceBusFailureReason.ServiceBusy);
        client.ScheduleFailure = failure;
        using var cancellation = new CancellationTokenSource();
        if (cancel) cancellation.Cancel();

        // Act
        var error = await Record.ExceptionAsync(async () =>
        {
            if (synchronous) syncChannel!.Requeue(message, TimeSpan.FromSeconds(5));
            else await asyncChannel!.RequeueAsync(message, TimeSpan.FromSeconds(5), cancellation.Token);
        });

        // Assert
        if (cancel) Assert.IsAssignableFrom<OperationCanceledException>(error);
        else Assert.Same(failure, error);
        Assert.Equal(0, client.Receiver.CompletionAttempts);
        Assert.Equal("original", message.Id.Value);
        Assert.Equal(originalBag, message.Header.Bag.ToArray());
        Assert.All(client.Senders.Skip(1), sender => Assert.True(sender.Disposed));
        if (cancel) Assert.Empty(client.Scheduled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_acknowledgement_fails_should_reuse_the_scheduled_retry_identity(bool synchronous)
    {
        // Arrange
        var (client, factory, subscription) = Create(synchronous);
        using var syncChannel = synchronous ? factory.CreateSyncChannel(subscription) : null;
        await using var asyncChannel = synchronous ? null : await factory.CreateAsyncChannelAsync(subscription);
        var message = synchronous ? syncChannel!.Receive(TimeSpan.FromSeconds(1))
            : await asyncChannel!.ReceiveAsync(TimeSpan.FromSeconds(1));
        message.Header.UpdateHandledCount();
        var failure = new ServiceBusException("Lock lost", ServiceBusFailureReason.MessageLockLost);
        client.Receiver.CompletionFailure = failure;

        // Act
        for (var i = 0; i < 2; i++)
        {
            var error = await Record.ExceptionAsync(async () =>
            {
                if (synchronous) syncChannel!.Requeue(message, TimeSpan.Zero);
                else await asyncChannel!.RequeueAsync(message, TimeSpan.Zero);
            });
            Assert.Same(failure, error);
        }

        // Assert
        Assert.Equal(2, client.Receiver.CompletionAttempts);
        Assert.Equal(2, client.Scheduled.Count);
        Assert.All(client.Scheduled, scheduled =>
        {
            Assert.Equal("retry-queue", scheduled.Queue);
            Assert.NotEqual("original", scheduled.Message.MessageId);
            Assert.Equal("original", scheduled.Message.ApplicationProperties[Message.OriginalMessageIdHeaderName]);
            Assert.Equal(new byte[] { 0, 255, 128, 1 }, scheduled.Message.Body.ToArray());
        });
        Assert.Equal(client.Scheduled[0].Message.MessageId, client.Scheduled[1].Message.MessageId);
        Assert.All(client.Senders.Skip(1), sender => Assert.True(sender.Disposed));
        Assert.Equal("original", message.Id.Value);
    }

    [Fact]
    public async Task When_a_scheduler_uses_another_broker_should_keep_retries_on_the_consuming_broker()
    {
        // Arrange
        var (local, factory, subscription) = Create(false);
        var (remote, remoteFactory, _) = Create(false);
        factory.Scheduler = remoteFactory.Scheduler;
        await using var channel = await factory.CreateAsyncChannelAsync(subscription);
        var message = await channel.ReceiveAsync(TimeSpan.FromSeconds(1));
        message.Header.UpdateHandledCount();

        // Act
        Assert.True(await channel.RequeueAsync(message, TimeSpan.FromSeconds(5)));

        // Assert
        Assert.Equal("retry-queue", Assert.Single(local.Scheduled).Queue);
        Assert.Empty(remote.Scheduled);
        Assert.Equal(1, local.Receiver.CompletionAttempts);
    }

    private static (InMemoryRetryServiceBusClient, AzureServiceBusChannelFactory, AzureServiceBusSubscription) Create(bool synchronous)
    {
        var delivery = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: new BinaryData(new byte[] { 0, 255, 128, 1 }), messageId: "original",
            lockTokenGuid: Guid.NewGuid(), lockedUntil: DateTimeOffset.UtcNow.AddMinutes(1),
            properties: new Dictionary<string, object> { ["MessageType"] = "MT_COMMAND" });
        var client = new InMemoryRetryServiceBusClient(delivery);
        var processor = new CommandProcessor(new SubscriberRegistry(),
            new SimpleHandlerFactoryAsync(_ => throw new InvalidOperationException()),
            new InMemoryRequestContextFactory(), new DefaultPolicy(),
            new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        var scheduler = new AzureServiceBusSchedulerFactory(client, new RoutingKey("scheduler")).Create(processor);
        var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(client)) { Scheduler = scheduler };
        var subscription = new AzureServiceBusSubscription(new SubscriptionName("retry"), new ChannelName("retry-queue"),
            new RoutingKey("retry-queue"), requestType: typeof(ASBTestCommand), timeOut: TimeSpan.FromSeconds(1),
            messagePumpType: synchronous ? MessagePumpType.Reactor : MessagePumpType.Proactor, makeChannels: OnMissingChannel.Assume,
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration { UseServiceBusQueue = true });
        return (client, factory, subscription);
    }
}
