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
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Trait("Category", "AzureServiceBus")]
[Collection("AzureServiceBus")]
public class AzureServiceBusDelayedRetryConfigurationTests
{
    [Theory]
    [InlineData(OnMissingChannel.Create, false)]
    [InlineData(OnMissingChannel.Create, true)]
    [InlineData(OnMissingChannel.Validate, false)]
    [InlineData(OnMissingChannel.Validate, true)]
    [InlineData(OnMissingChannel.Assume, false)]
    [InlineData(OnMissingChannel.Assume, true)]
    public async Task When_configuring_delayed_topic_retries_should_require_a_dedicated_queue(
        OnMissingChannel makeChannels, bool synchronous)
    {
        // Arrange
        var provider = ASBCreds.ASBClientProvider;
        var administration = provider.GetServiceBusAdministrationClient();
        var topicName = $"unsafe-retry-{Guid.NewGuid():N}";
        var factory = new AzureServiceBusConsumerFactory(provider);
        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
            channelName: new ChannelName("a"), routingKey: new RoutingKey(topicName),
            requeueCount: 3, requeueDelay: TimeSpan.FromSeconds(2), makeChannels: makeChannels);

        // Act
        var exception = Assert.Throws<ConfigurationException>(() =>
        {
            if (synchronous)
            {
                using var consumer = factory.Create(subscription);
            }
            else
            {
                var consumer = factory.CreateAsync(subscription);
                ((IDisposable)consumer).Dispose();
            }
        });

        // Assert
        Assert.Contains("ForwardTo", exception.Message);
        Assert.False((await administration.TopicExistsAsync(topicName)).Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_requesting_a_delayed_topic_retry_should_reject_without_publishing_or_settling(bool synchronous)
    {
        // Arrange
        var provider = ASBCreds.ASBClientProvider;
        await using var client = provider.GetServiceBusClient();
        var administration = provider.GetServiceBusAdministrationClient();
        var topicName = $"unsafe-message-retry-{Guid.NewGuid():N}";
        await administration.CreateTopicAsync(topicName);
        try
        {
            foreach (var name in new[] { "a", "b", "c" })
            {
                await administration.CreateSubscriptionAsync(new CreateSubscriptionOptions(topicName, name)
                {
                    DefaultMessageTimeToLive = TimeSpan.FromHours(1)
                });
            }
            var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(provider));
            var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
                channelName: new ChannelName("a"), routingKey: new RoutingKey(topicName),
                makeChannels: OnMissingChannel.Validate);
            using var syncChannel = synchronous ? factory.CreateSyncChannel(subscription) : null;
            await using var asyncChannel = synchronous ? null : await factory.CreateAsyncChannelAsync(subscription);
            await using var otherB = client.CreateReceiver(topicName, "b");
            await using var otherC = client.CreateReceiver(topicName, "c");
            await using var sender = client.CreateSender(topicName);
            var original = new ServiceBusMessage("retry me") { MessageId = Guid.NewGuid().ToString() };
            original.ApplicationProperties["MessageType"] = "MT_COMMAND";
            await sender.SendMessageAsync(original);
            var received = synchronous
                ? syncChannel!.Receive(TimeSpan.FromSeconds(10))
                : await asyncChannel!.ReceiveAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(original.MessageId, received.Id.Value);
            var receivedB = await otherB.ReceiveMessageAsync(TimeSpan.FromSeconds(10));
            var receivedC = await otherC.ReceiveMessageAsync(TimeSpan.FromSeconds(10));
            Assert.NotNull(receivedB);
            Assert.NotNull(receivedC);
            await otherB.CompleteMessageAsync(receivedB);
            await otherC.CompleteMessageAsync(receivedC);

            // Act
            received.Header.UpdateHandledCount();
            var exception = synchronous
                ? Assert.Throws<ConfigurationException>(() => syncChannel!.Requeue(received, TimeSpan.FromSeconds(2)))
                : await Assert.ThrowsAsync<ConfigurationException>(() => asyncChannel!.RequeueAsync(received, TimeSpan.FromSeconds(2)));

            // Assert
            Assert.Contains("ForwardTo", exception.Message);
            if (synchronous)
                syncChannel!.Acknowledge(received);
            else
                await asyncChannel!.AcknowledgeAsync(received);
            Assert.Null(await otherB.ReceiveMessageAsync(TimeSpan.FromSeconds(3)));
            Assert.Null(await otherC.ReceiveMessageAsync(TimeSpan.FromSeconds(3)));
        }
        finally
        {
            await administration.DeleteTopicAsync(topicName);
        }
    }
}
