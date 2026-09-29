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

#nullable enable

using System;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Property("Category", "ASB")]
public class AzureServiceBusNativePartitionKeyPublishingTests
{
    [Test]
    [Arguments("101", null, "101")]
    [Arguments(" ", null, " ")]
    [Arguments("", null, null)]
    [Arguments(null, null, null)]
    [Arguments("101", "101", "101")]
    [Arguments("101", "session-42", "session-42")]
    [Arguments("", "session-42", null)]
    [Arguments(null, "session-42", null)]
    public async System.Threading.Tasks.Task When_publishing_a_message_should_map_its_native_partition_key(
        string? partitionKey, string? sessionId, string? expectedNativeKey)
    {
        // Arrange
        var header = new MessageHeader(Id.Random(), new RoutingKey("orders"), MessageType.MT_EVENT,
            partitionKey: partitionKey is null ? null : new PartitionKey(partitionKey));
        if (sessionId is not null)
            header.Bag["SessionId"] = sessionId;
        var message = new Message(header, new MessageBody("{}"));

        // Act
        var published = AzureServiceBusMessagePublisher.ConvertToServiceBusMessage(message);

        // Assert
        await Assert.That(published.PartitionKey).IsEqualTo(expectedNativeKey);
        await Assert.That(published.SessionId).IsEqualTo(sessionId);
        await Assert.That(published.ApplicationProperties["cloudEvents:partitionkey"]).IsEqualTo(partitionKey ?? string.Empty);
    }

    [Test]
    public async System.Threading.Tasks.Task When_partition_key_has_maximum_native_length_should_publish_it()
    {
        // Arrange
        var partitionKey = new string('a', 128);
        var header = new MessageHeader(Id.Random(), new RoutingKey("orders"), MessageType.MT_EVENT,
            partitionKey: new PartitionKey(partitionKey));
        var message = new Message(header, new MessageBody("{}"));

        // Act
        var published = AzureServiceBusMessagePublisher.ConvertToServiceBusMessage(message);

        // Assert
        await Assert.That(published.PartitionKey).IsEqualTo(partitionKey);
        await Assert.That(published.ApplicationProperties["cloudEvents:partitionkey"]).IsEqualTo(partitionKey);
    }

    [Test]
    public async System.Threading.Tasks.Task When_partition_key_exceeds_native_length_limit_should_reject_it()
    {
        // Arrange
        var header = new MessageHeader(Id.Random(), new RoutingKey("orders"), MessageType.MT_EVENT,
            partitionKey: new PartitionKey(new string('a', 129)));
        var message = new Message(header, new MessageBody("{}"));

        // Act
        var exception = await Assert.That(() => AzureServiceBusMessagePublisher.ConvertToServiceBusMessage(message)).ThrowsExactly<ArgumentOutOfRangeException>();

        // Assert
        await Assert.That(exception.ParamName).IsEqualTo("value");
    }
}
