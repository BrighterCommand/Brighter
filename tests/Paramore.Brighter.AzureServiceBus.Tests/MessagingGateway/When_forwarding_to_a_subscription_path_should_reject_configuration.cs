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

using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusForwardingDestinationValidationTests
{
    [Test]
    [Arguments("orders/Subscriptions/accounting")]
    [Arguments("orders/subscriptions/accounting")]
    [Arguments("orders/$DeadLetterQueue")]
    [Arguments("sb://namespace/queue")]
    [Arguments("/queue")]
    [Arguments("queue/")]
    [Arguments("queue//child")]
    [Arguments("queue/ /child")]
    public async Task When_forwarding_to_a_subscription_path_should_reject_configuration(string destination)
    {
        // Arrange
        await using var client = new InMemoryRetryServiceBusClient(ServiceBusModelFactory.ServiceBusReceivedMessage());
        var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(client));
        var subscription = new AzureServiceBusSubscription(new SubscriptionName("accounting"),
            new ChannelName("accounting"), new RoutingKey("orders"),
            requestType: typeof(Command), timeOut: System.TimeSpan.FromSeconds(1),
            messagePumpType: MessagePumpType.Proactor, makeChannels: OnMissingChannel.Assume,
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration { ForwardTo = destination });

        // Act
        var error = await TestExceptionRecorder.CaptureAsync(async () =>
        {
            await using var channel = await factory.CreateAsyncChannelAsync(subscription);
        });

        // Assert
        await Assert.That(error).IsTypeOf<ConfigurationException>();
        await Assert.That(client.Scheduled).IsEmpty();
        await Assert.That(client.Senders).IsEmpty();
    }

    [Test]
    [Arguments("orders/Subscriptions/accounting")]
    [Arguments("orders/subscriptions/accounting")]
    [Arguments("orders/$DeadLetterQueue")]
    [Arguments("sb://namespace/queue")]
    [Arguments("/queue")]
    [Arguments("queue/")]
    [Arguments("queue//child")]
    [Arguments("queue/ /child")]
    public async Task When_scheduling_to_a_subscription_path_should_reject_before_creating_a_sender(string destination)
    {
        // Arrange
        await using var client = new InMemoryRetryServiceBusClient(ServiceBusModelFactory.ServiceBusReceivedMessage());
        var scheduler = new AzureServiceBusRequeueScheduler(client);
        var message = new Message(new MessageHeader(Id.Random(), new RoutingKey("orders"), MessageType.MT_COMMAND),
            new MessageBody("order"));

        // Act
        var error = await TestExceptionRecorder.CaptureAsync(() => scheduler.RequeueAsync(message, new ChannelName(destination), System.TimeSpan.Zero));

        // Assert
        await Assert.That(error).IsTypeOf<System.ArgumentException>();
        await Assert.That(client.Scheduled).IsEmpty();
        await Assert.That(client.Senders).IsEmpty();
    }

    [Test]
    [Arguments("orders/Subscriptions/accounting", false)]
    [Arguments("orders/Subscriptions/accounting", true)]
    [Arguments("queue/$DeadLetterQueue", false)]
    [Arguments("sb://namespace/queue", true)]
    [Arguments("/queue", false)]
    [Arguments("queue/", true)]
    [Arguments("queue//child", false)]
    [Arguments("queue/ /child", true)]
    public async Task When_using_an_invalid_queue_routing_key_should_reject_configuration(string destination, bool synchronous)
    {
        // Arrange
        await using var client = new InMemoryRetryServiceBusClient(ServiceBusModelFactory.ServiceBusReceivedMessage());
        var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(client));
        var subscription = new AzureServiceBusSubscription(new SubscriptionName("retry"),
            new ChannelName("retry"), new RoutingKey(destination), requestType: typeof(Command),
            timeOut: System.TimeSpan.FromSeconds(1),
            messagePumpType: synchronous ? MessagePumpType.Reactor : MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Assume,
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration { UseServiceBusQueue = true });

        // Act
        var error = await TestExceptionRecorder.CaptureAsync(async () =>
        {
            if (synchronous)
            {
                using var channel = factory.CreateSyncChannel(subscription);
            }
            else
            {
                await using var channel = await factory.CreateAsyncChannelAsync(subscription);
            }
        });

        // Assert
        await Assert.That(error).IsTypeOf<ConfigurationException>();
        await Assert.That(client.Senders).IsEmpty();
    }
}
