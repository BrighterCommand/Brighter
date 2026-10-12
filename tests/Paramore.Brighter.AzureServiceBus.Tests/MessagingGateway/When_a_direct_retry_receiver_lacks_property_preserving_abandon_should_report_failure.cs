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
using Paramore.Brighter.AzureServiceBus.Tests.Fakes;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusDirectRetryReceiverCapabilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_a_direct_retry_receiver_lacks_property_preserving_abandon_should_report_failure(bool synchronous)
    {
        // Arrange
        var receiver = new FakeServiceBusReceiverWrapper
        {
            MessageQueue =
            [
                new BrokeredMessage
                {
                    Id = "original", LockToken = Guid.NewGuid().ToString(),
                    MessageBodyValue = new byte[] { 1 }, ContentType = "application/octet-stream",
                    ApplicationProperties = new Dictionary<string, object> { ["MessageType"] = "MT_COMMAND" }
                }
            ]
        };
        var subscription = new AzureServiceBusSubscription(new SubscriptionName("retry"),
            new ChannelName("consumer"), new RoutingKey("orders"), requestType: typeof(Command),
            messagePumpType: synchronous ? MessagePumpType.Reactor : MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Assume);
        using var consumer = new AzureServiceBusTopicConsumer(subscription,
            new FakeAdministrationClient(), new FakeServiceBusReceiverProvider(receiver));
        var received = synchronous ? consumer.Receive(TimeSpan.FromSeconds(1))
            : await consumer.ReceiveAsync(TimeSpan.FromSeconds(1));
        var message = Assert.Single(received);
        message.Header.UpdateHandledCount();

        // Act
        var error = await Record.ExceptionAsync(async () =>
        {
            if (synchronous) consumer.Requeue(message, TimeSpan.Zero);
            else await consumer.RequeueAsync(message, TimeSpan.Zero);
        });

        // Assert
        Assert.Contains("IAmAServiceBusRetryReceiver", Assert.IsType<ChannelFailureException>(error).Message);
        Assert.Equal("original", message.Id.Value);
    }
}
