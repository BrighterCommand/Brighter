#region Licence
/* The MIT License (MIT)
Copyright © 2026 gabisonia <irakli.gabisonia94@gmail.com>

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

using System.Threading.Tasks;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class KafkaConsumerRepeatedDisposalTests
{
    [Theory]
    [InlineData("requeue", false, false)]
    [InlineData("requeue", true, true)]
    [InlineData("requeue", false, true)]
    [InlineData("requeue", true, false)]
    [InlineData("dead-letter", false, false)]
    [InlineData("dead-letter", true, true)]
    [InlineData("dead-letter", false, true)]
    [InlineData("dead-letter", true, false)]
    [InlineData("invalid", false, false)]
    [InlineData("invalid", true, true)]
    [InlineData("invalid", false, true)]
    [InlineData("invalid", true, false)]
    [InlineData("all", false, false)]
    [InlineData("all", true, true)]
    [InlineData("all", false, true)]
    [InlineData("all", true, false)]
    public async Task When_a_kafka_consumer_with_owned_producers_is_disposed_repeatedly_should_not_throw(
        string ownedProducers, bool firstDisposeAsync, bool subsequentDisposeAsync)
    {
        // Arrange
        await using var host = new KafkaDisposalTestHost();
        if (ownedProducers is "requeue" or "all")
        {
            var message = await host.ReceiveMessageAsync();
            Assert.True(await host.Consumer.RequeueAsync(message));
        }

        if (ownedProducers is "dead-letter" or "all")
        {
            var message = await host.ReceiveMessageAsync();
            Assert.True(await host.Consumer.RejectAsync(message,
                new MessageRejectionReason(RejectionReason.DeliveryError)));
        }

        if (ownedProducers is "invalid" or "all")
        {
            var message = await host.ReceiveMessageAsync();
            Assert.True(await host.Consumer.RejectAsync(message,
                new MessageRejectionReason(RejectionReason.Unacceptable)));
        }

        if (firstDisposeAsync)
            await host.DisposeAsync();
        else
            host.Dispose();

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (subsequentDisposeAsync)
                    await host.Consumer.DisposeAsync();
                else
                    host.Consumer.Dispose();
            }
        });

        // Assert
        Assert.Null(exception);
    }
}
