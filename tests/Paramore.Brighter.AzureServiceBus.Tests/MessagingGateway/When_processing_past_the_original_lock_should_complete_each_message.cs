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
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Trait("Category", "ASB")]
public class AzureServiceBusLockRenewalTests
{
    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 1)]
    [InlineData(false, false, 4)]
    [InlineData(false, true, 4)]
    [InlineData(true, false, 4)]
    [InlineData(true, true, 4)]
    [InlineData(false, false, 1, true)]
    [InlineData(false, true, 1, true)]
    [InlineData(true, false, 1, true)]
    [InlineData(true, true, 1, true)]
    [InlineData(false, false, 4, true)]
    [InlineData(false, true, 4, true)]
    [InlineData(true, false, 4, true)]
    [InlineData(true, true, 4, true)]
    public async Task When_processing_past_the_original_lock_should_complete_each_message(
        bool useQueue, bool useAsync, int bufferSize, bool useSession = false)
    {
        // Arrange
        var lockDuration = TimeSpan.FromSeconds(2);
        var handlingDuration = TimeSpan.FromSeconds(bufferSize == 1 ? 3 : 1);
        var receiver = new InMemoryLockingServiceBusReceiver(bufferSize, lockDuration);
        var sessionReceiver = new InMemoryLockingServiceBusSessionReceiver(receiver, lockDuration);
        await using var client = new InMemoryLockingServiceBusClient(useSession ? sessionReceiver : receiver);
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            subscriptionName: new SubscriptionName("lock-renewal"),
            channelName: new ChannelName("lock-renewal"),
            routingKey: new RoutingKey("lock-renewal"),
            bufferSize: bufferSize,
            makeChannels: OnMissingChannel.Assume,
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration
            {
                UseServiceBusQueue = useQueue,
                RequireSession = useSession,
                LockDuration = lockDuration
            });
        var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(client));
        var receivedIds = new HashSet<Id>();

        // Act
        if (useAsync)
        {
            await using var channel = factory.CreateAsyncChannel(subscription);
            for (var i = 0; i < bufferSize; i++)
            {
                var message = await channel.ReceiveAsync(TimeSpan.FromSeconds(1));
                Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
                Assert.True(receivedIds.Add(message.Id), "Each message must be dispatched only once.");
                await Task.Delay(handlingDuration);
                await channel.AcknowledgeAsync(message);
            }
        }
        else
        {
            using var channel = factory.CreateSyncChannel(subscription);
            for (var i = 0; i < bufferSize; i++)
            {
                var message = channel.Receive(TimeSpan.FromSeconds(1));
                Assert.Equal(MessageType.MT_COMMAND, message.Header.MessageType);
                Assert.True(receivedIds.Add(message.Id), "Each message must be dispatched only once.");
                await Task.Delay(handlingDuration);
                channel.Acknowledge(message);
            }
        }

        // Assert
        Assert.Equal(bufferSize, receiver.CompletedCount);
        Assert.True((useSession ? sessionReceiver.RenewalCount : receiver.RenewalCount) > 0, "Completion beyond the original lock requires renewal.");
    }
}
