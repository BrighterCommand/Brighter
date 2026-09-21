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

using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

[Trait("Category", "ASB")]
public class AzureServiceBusNativeSubjectPublishingTests
{
    [Theory]
    [InlineData("order-placed")]
    [InlineData("შეკვეთა/注文")]
    [InlineData(" ")]
    [InlineData("")]
    [InlineData(null)]
    public void When_publishing_a_message_should_map_its_native_subject(string? subject)
    {
        // Arrange
        var header = new MessageHeader(Id.Random(), new RoutingKey("orders"), MessageType.MT_EVENT,
            subject: subject);
        var message = new Message(header, new MessageBody("{}"));

        // Act
        var published = AzureServiceBusMessagePublisher.ConvertToServiceBusMessage(message);

        // Assert
        if (string.IsNullOrEmpty(subject))
        {
            Assert.Null(published.Subject);
            Assert.False(published.ApplicationProperties.ContainsKey("cloudEvents:subject"));
        }
        else
        {
            Assert.Equal(subject, published.Subject);
            Assert.Equal(subject, published.ApplicationProperties["cloudEvents:subject"]);
        }
    }
}
