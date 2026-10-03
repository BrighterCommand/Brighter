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
public class AzureServiceBusBrokerLockRenewalTests
{
    [Fact]
    public async Task When_processing_beyond_the_broker_lock_should_remove_the_message()
    {
        // Arrange
        var provider = ASBCreds.ASBClientProvider;
        await using var client = provider.GetServiceBusClient();
        var administration = provider.GetServiceBusAdministrationClient();
        var queueName = $"lock-renewal-{Guid.NewGuid():N}";
        await administration.CreateQueueAsync(new CreateQueueOptions(queueName)
        {
            LockDuration = TimeSpan.FromSeconds(5),
            MaxDeliveryCount = 3
        });

        try
        {
            await using var sender = client.CreateSender(queueName);
            var original = new ServiceBusMessage("{}") { MessageId = Guid.NewGuid().ToString() };
            original.ApplicationProperties["MessageType"] = "MT_COMMAND";
            await sender.SendMessageAsync(original);
            var subscription = new AzureServiceBusSubscription<ASBTestCommand>(
                channelName: new ChannelName(queueName), routingKey: new RoutingKey(queueName),
                makeChannels: OnMissingChannel.Assume,
                subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration { UseServiceBusQueue = true });
            var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(provider));
            await using var channel = factory.CreateAsyncChannel(subscription);

            // Act
            var message = await channel.ReceiveAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(original.MessageId, message.Id.Value);
            await Task.Delay(TimeSpan.FromSeconds(15));
            await channel.AcknowledgeAsync(message);

            // Assert
            await using var observer = client.CreateReceiver(queueName);
            Assert.Null(await observer.PeekMessageAsync());
            await using var deadLetters = client.CreateReceiver(queueName,
                new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
            Assert.Null(await deadLetters.PeekMessageAsync());
        }
        finally
        {
            await administration.DeleteQueueAsync(queueName);
        }
    }
}
