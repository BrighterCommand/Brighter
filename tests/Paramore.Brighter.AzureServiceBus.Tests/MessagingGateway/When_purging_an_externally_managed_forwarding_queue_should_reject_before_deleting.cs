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
public class AzureServiceBusForwardingPurgeTests
{
    [Theory]
    [InlineData(OnMissingChannel.Validate, false)]
    [InlineData(OnMissingChannel.Validate, true)]
    [InlineData(OnMissingChannel.Assume, false)]
    [InlineData(OnMissingChannel.Assume, true)]
    public async Task When_purging_an_externally_managed_forwarding_queue_should_reject_before_deleting(
        OnMissingChannel makeChannels, bool synchronous)
    {
        // Arrange
        var provider = ASBCreds.ASBClientProvider;
        await using var client = provider.GetServiceBusClient();
        var administration = provider.GetServiceBusAdministrationClient();
        var topicName = $"purge-topic-{Guid.NewGuid():N}";
        var queueName = $"purge-queue-{Guid.NewGuid():N}";
        await administration.CreateTopicAsync(topicName);
        try
        {
            await administration.CreateQueueAsync(new CreateQueueOptions(queueName)
            {
                DefaultMessageTimeToLive = TimeSpan.FromHours(1)
            });
            await administration.CreateSubscriptionAsync(new CreateSubscriptionOptions(topicName, "a")
            {
                DefaultMessageTimeToLive = TimeSpan.FromHours(1),
                ForwardTo = queueName
            });
            var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(provider));
            var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
                channelName: new ChannelName("a"), routingKey: new RoutingKey(topicName),
                makeChannels: makeChannels,
                subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration { ForwardTo = queueName });
            using var syncChannel = synchronous ? factory.CreateSyncChannel(subscription) : null;
            await using var asyncChannel = synchronous ? null : await factory.CreateAsyncChannelAsync(subscription);
            await using var sender = client.CreateSender(topicName);
            var original = new ServiceBusMessage("before purge") { MessageId = Guid.NewGuid().ToString() };
            original.ApplicationProperties["MessageType"] = "MT_COMMAND";
            await sender.SendMessageAsync(original);
            var received = synchronous
                ? syncChannel!.Receive(TimeSpan.FromSeconds(10))
                : await asyncChannel!.ReceiveAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(original.MessageId, received.Id.Value);

            // Act
            var exception = synchronous
                ? Assert.Throws<NotSupportedException>(() => syncChannel!.Purge())
                : await Assert.ThrowsAsync<NotSupportedException>(() => asyncChannel!.PurgeAsync());

            // Assert
            Assert.Contains("Create", exception.Message);
            Assert.True((await administration.QueueExistsAsync(queueName)).Value);
            Assert.EndsWith(queueName, (await administration.GetSubscriptionAsync(topicName, "a")).Value.ForwardTo);
            if (synchronous)
                syncChannel!.Acknowledge(received);
            else
                await asyncChannel!.AcknowledgeAsync(received);
        }
        finally
        {
            await administration.DeleteTopicAsync(topicName);
            if ((await administration.QueueExistsAsync(queueName)).Value)
                await administration.DeleteQueueAsync(queueName);
        }
    }
}
