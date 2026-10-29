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

using System;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class KafkaProducerRepeatedDisposalTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task When_a_kafka_producer_is_disposed_repeatedly_should_not_throw(
        bool firstDisposeAsync, bool subsequentDisposeAsync)
    {
        // Arrange
        var producer = new KafkaMessageProducer(
            new KafkaMessagingGatewayConfiguration
            {
                Name = "producer-disposal",
                BootStrapServers = ["localhost:9092"]
            },
            new KafkaPublication
            {
                Topic = new RoutingKey(Guid.NewGuid().ToString()),
                MakeChannels = OnMissingChannel.Assume
            });
        producer.Init();

        if (firstDisposeAsync)
            await producer.DisposeAsync();
        else
            producer.Dispose();

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (subsequentDisposeAsync)
                    await producer.DisposeAsync();
                else
                    producer.Dispose();
            }
        });

        // Assert
        Assert.Null(exception);
    }
}
