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
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.AzureServiceBus.Tests.Fakes;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusManualRetrySchedulerValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_constructing_a_queue_consumer_with_an_incompatible_scheduler_should_reject_configuration(bool missing)
    {
        // Arrange
        await using var client = new InMemoryRetryServiceBusClient(ServiceBusModelFactory.ServiceBusReceivedMessage());
        var processor = new CommandProcessor(new SubscriberRegistry(),
            new SimpleHandlerFactoryAsync(_ => throw new InvalidOperationException()),
            new InMemoryRequestContextFactory(), new DefaultPolicy(),
            new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        var scheduler = missing ? null : new InMemorySchedulerFactory().Create(processor);
        var subscription = new AzureServiceBusSubscription(new SubscriptionName("retry"),
            new ChannelName("queue"), new RoutingKey("queue"), requestType: typeof(Command),
            messagePumpType: MessagePumpType.Proactor, makeChannels: OnMissingChannel.Assume,
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration { UseServiceBusQueue = true });

        // Act
        var error = Record.Exception(() =>
        {
            using var consumer = new AzureServiceBusQueueConsumer(subscription, scheduler!,
                new AdministrationClientWrapper(client), new FakeServiceBusReceiverProvider(null));
        });

        // Assert
        Assert.IsType<ConfigurationException>(error);
        Assert.Empty(client.Scheduled);
        Assert.Empty(client.Senders);
    }
}
