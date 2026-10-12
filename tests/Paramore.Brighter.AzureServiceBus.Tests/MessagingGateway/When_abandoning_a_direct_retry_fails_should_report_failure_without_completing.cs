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
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusDirectRetryAbandonFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_abandoning_a_direct_retry_fails_should_report_failure_without_completing(bool synchronous)
    {
        // Arrange
        var failure = new ServiceBusException("Lock lost", ServiceBusFailureReason.MessageLockLost);
        var delivery = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: new BinaryData("order"), messageId: "original", lockTokenGuid: Guid.NewGuid(),
            lockedUntil: DateTimeOffset.UtcNow.AddMinutes(1),
            properties: new Dictionary<string, object> { ["MessageType"] = "MT_COMMAND" });
        var receiver = new InMemoryRetryServiceBusReceiver(delivery) { AbandonFailure = failure };
        await using var client = new InMemoryLockingServiceBusClient(receiver);
        var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(client));
        var subscription = new AzureServiceBusSubscription(new SubscriptionName("retry"),
            new ChannelName("consumer"), new RoutingKey("orders"), requestType: typeof(Command),
            timeOut: TimeSpan.FromSeconds(1),
            messagePumpType: synchronous ? MessagePumpType.Reactor : MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Assume);
        using var syncChannel = synchronous ? factory.CreateSyncChannel(subscription) : null;
        await using var asyncChannel = synchronous ? null : await factory.CreateAsyncChannelAsync(subscription);
        var message = synchronous ? syncChannel!.Receive(TimeSpan.FromSeconds(1))
            : await asyncChannel!.ReceiveAsync(TimeSpan.FromSeconds(1));
        message.Header.UpdateHandledCount();

        // Act
        var error = await Record.ExceptionAsync(async () =>
        {
            if (synchronous) syncChannel!.Requeue(message, TimeSpan.Zero);
            else await asyncChannel!.RequeueAsync(message, TimeSpan.Zero);
        });

        // Assert
        Assert.Same(failure, error);
        Assert.Equal(1, receiver.AbandonAttempts);
        Assert.Equal(0, receiver.CompletionAttempts);
        Assert.Equal("original", message.Id.Value);
    }
}
