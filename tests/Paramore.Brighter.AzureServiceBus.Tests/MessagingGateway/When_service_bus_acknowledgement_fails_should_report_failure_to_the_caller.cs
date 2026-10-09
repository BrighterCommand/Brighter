#region Licence
/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.AzureServiceBus.Tests.Fakes;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class ServiceBusAcknowledgementFailureTests
{
    [Theory]
    [InlineData(false, false, ServiceBusFailureReason.MessageLockLost)]
    [InlineData(false, true, ServiceBusFailureReason.MessageLockLost)]
    [InlineData(true, false, ServiceBusFailureReason.MessageLockLost)]
    [InlineData(true, true, ServiceBusFailureReason.MessageLockLost)]
    [InlineData(false, false, ServiceBusFailureReason.ServiceBusy)]
    [InlineData(false, true, ServiceBusFailureReason.ServiceBusy)]
    [InlineData(true, false, ServiceBusFailureReason.ServiceBusy)]
    [InlineData(true, true, ServiceBusFailureReason.ServiceBusy)]
    public async Task When_service_bus_acknowledgement_fails_should_report_failure_to_the_caller(
        bool useAsync, bool aggregateException, ServiceBusFailureReason reason)
    {
        //Arrange
        var failure = new ServiceBusException("Message was not completed", reason);
        var receiver = new FakeServiceBusReceiverWrapper
        {
            CompleteException = aggregateException ? new AggregateException(failure) : failure,
            MessageQueue =
            [
                new BrokeredMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    LockToken = "claim-lock",
                    ContentType = "text/plain",
                    MessageBodyValue = Encoding.UTF8.GetBytes("Claim Check stored-payload"),
                    ApplicationProperties = new Dictionary<string, object> { ["MessageType"] = "MT_COMMAND" }
                }
            ]
        };
        var subscription = new AzureServiceBusSubscription(
            new SubscriptionName("claim-subscription"), new ChannelName("claim-channel"), new RoutingKey("claims"),
            requestType: typeof(Command),
            messagePumpType: useAsync ? MessagePumpType.Proactor : MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Assume,
            subscriptionConfiguration: new AzureServiceBusSubscriptionConfiguration { UseServiceBusQueue = true });
        using var consumer = new AzureServiceBusQueueConsumer(subscription, new FakeMessageProducer(),
            new FakeAdministrationClient(), new FakeServiceBusReceiverProvider(receiver));
        var received = useAsync
            ? await consumer.ReceiveAsync(TimeSpan.FromMilliseconds(1))
            : consumer.Receive(TimeSpan.FromMilliseconds(1));
        var message = Assert.Single(received);

        //Act
        var error = await Record.ExceptionAsync(async () =>
        {
            if (useAsync)
                await consumer.AcknowledgeAsync(message);
            else
                consumer.Acknowledge(message);
        });

        //Assert
        Assert.NotNull(error);
        Assert.Same(failure, error.GetBaseException());
    }
}
