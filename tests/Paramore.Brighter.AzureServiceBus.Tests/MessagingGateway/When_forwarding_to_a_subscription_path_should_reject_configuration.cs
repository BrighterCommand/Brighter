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
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusForwardingDestinationValidationTests
{
    [Theory]
    [InlineData("orders/Subscriptions/accounting")]
    [InlineData("orders/subscriptions/accounting")]
    [InlineData("orders/$DeadLetterQueue")]
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
        var error = await Record.ExceptionAsync(async () =>
        {
            await using var channel = await factory.CreateAsyncChannelAsync(subscription);
        });

        // Assert
        Assert.IsType<ConfigurationException>(error);
        Assert.Empty(client.Scheduled);
        Assert.Empty(client.Senders);
    }

    [Theory]
    [InlineData("orders/Subscriptions/accounting")]
    [InlineData("orders/subscriptions/accounting")]
    [InlineData("orders/$DeadLetterQueue")]
    public async Task When_scheduling_to_a_subscription_path_should_reject_before_creating_a_sender(string destination)
    {
        // Arrange
        await using var client = new InMemoryRetryServiceBusClient(ServiceBusModelFactory.ServiceBusReceivedMessage());
        var scheduler = new AzureServiceBusRequeueScheduler(client);
        var message = new Message(new MessageHeader(Id.Random(), new RoutingKey("orders"), MessageType.MT_COMMAND),
            new MessageBody("order"));

        // Act
        var error = await Record.ExceptionAsync(() => scheduler.RequeueAsync(message, new ChannelName(destination), System.TimeSpan.Zero));

        // Assert
        Assert.IsType<System.ArgumentException>(error);
        Assert.Empty(client.Scheduled);
        Assert.Empty(client.Senders);
    }
}
