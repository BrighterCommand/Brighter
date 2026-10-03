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
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusQueueValidationTests
{
    [Theory]
    [InlineData(false, "name")]
    [InlineData(false, "idle")]
    [InlineData(true, "name")]
    [InlineData(true, "idle")]
    [InlineData(true, "delivery-count")]
    [InlineData(true, "lock")]
    [InlineData(true, "lifetime")]
    public async Task When_queue_settings_are_invalid_should_return_a_faulted_task(bool useConfiguration, string invalidSetting)
    {
        // Arrange
        var administration = new InMemoryServiceBusAdministrationClient();
        await using var client = new InMemoryServiceBusClient(ServiceBusModelFactory.ServiceBusReceivedMessage(), administration);
        var wrapper = new AdministrationClientWrapper(client);
        var queueName = invalidSetting == "name" ? string.Empty : "orders";
        var idleTimeout = invalidSetting == "idle" ? TimeSpan.Zero : TimeSpan.FromMinutes(15);
        var configuration = new AzureServiceBusSubscriptionConfiguration
        {
            QueueIdleBeforeDelete = idleTimeout,
            MaxDeliveryCount = invalidSetting == "delivery-count" ? 0 : 5,
            LockDuration = invalidSetting == "lock" ? TimeSpan.Zero : TimeSpan.FromMinutes(1),
            DefaultMessageTimeToLive = invalidSetting == "lifetime" ? TimeSpan.Zero : TimeSpan.FromDays(3)
        };
        Task creation = Task.CompletedTask;

        // Act
        var synchronousException = Record.Exception(() =>
        {
            creation = useConfiguration
                ? wrapper.CreateQueueAsync(queueName, configuration)
                : wrapper.CreateQueueAsync(queueName, idleTimeout, 1024);
        });

        // Assert
        Assert.Null(synchronousException);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => creation);
        Assert.Empty(administration.Queues);
    }
}
