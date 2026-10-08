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
using Azure.Messaging.ServiceBus.Administration;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Trait("Category", "AzureServiceBus")]
[Collection("AzureServiceBus")]
public class AzureServiceBusForwardingMigrationTests
{
    [Theory]
    [InlineData(OnMissingChannel.Create)]
    [InlineData(OnMissingChannel.Validate)]
    public async Task When_an_existing_subscription_does_not_forward_should_require_explicit_migration(
        OnMissingChannel makeChannels)
    {
        // Arrange
        var provider = ASBCreds.ASBClientProvider;
        await using var client = provider.GetServiceBusClient();
        var administration = provider.GetServiceBusAdministrationClient();
        var topicName = $"migration-{Guid.NewGuid():N}";
        var queueName = $"migration-queue-{Guid.NewGuid():N}";
        await administration.CreateTopicAsync(topicName);

        try
        {
            await administration.CreateQueueAsync(new CreateQueueOptions(queueName)
            {
                DefaultMessageTimeToLive = TimeSpan.FromHours(1)
            });
            await administration.CreateSubscriptionAsync(new CreateSubscriptionOptions(topicName, "a")
            {
                DefaultMessageTimeToLive = TimeSpan.FromHours(1)
            });
            var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(provider));
            var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
                subscriptionName: new SubscriptionName("a"),
                channelName: new ChannelName("a"),
                routingKey: new RoutingKey(topicName),
                messagePumpType: MessagePumpType.Proactor,
                makeChannels: makeChannels,
                subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration { ForwardTo = queueName });

            // Act
            var exception = await Assert.ThrowsAsync<ChannelFailureException>(
                () => factory.CreateAsyncChannelAsync(subscription));

            // Assert
            Assert.Contains(queueName, exception.Message);
            Assert.True(string.IsNullOrEmpty((await administration.GetSubscriptionAsync(topicName, "a")).Value.ForwardTo));
        }
        finally
        {
            await administration.DeleteTopicAsync(topicName);
            if ((await administration.QueueExistsAsync(queueName)).Value)
                await administration.DeleteQueueAsync(queueName);
        }
    }
}
