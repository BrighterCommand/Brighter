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

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

[Property("Category", "Kafka")]
[NotInParallel("Kafka")]
public class KafkaConsumerRepeatedDisposalTests
{
    [Test]
    [Arguments("requeue", false, false)]
    [Arguments("requeue", true, true)]
    [Arguments("requeue", false, true)]
    [Arguments("requeue", true, false)]
    [Arguments("dead-letter", false, false)]
    [Arguments("dead-letter", true, true)]
    [Arguments("dead-letter", false, true)]
    [Arguments("dead-letter", true, false)]
    [Arguments("invalid", false, false)]
    [Arguments("invalid", true, true)]
    [Arguments("invalid", false, true)]
    [Arguments("invalid", true, false)]
    [Arguments("all", false, false)]
    [Arguments("all", true, true)]
    [Arguments("all", false, true)]
    [Arguments("all", true, false)]
    public async Task When_a_kafka_consumer_with_owned_producers_is_disposed_repeatedly_should_not_throw(
        string ownedProducers, bool firstDisposeAsync, bool subsequentDisposeAsync)
    {
        // Arrange
        await using var host = new KafkaDisposalTestHost();
        if (ownedProducers is "requeue" or "all")
        {
            var message = await host.ReceiveMessageAsync();
            await Assert.That(await host.Consumer.RequeueAsync(message)).IsTrue();
        }

        if (ownedProducers is "dead-letter" or "all")
        {
            var message = await host.ReceiveMessageAsync();
            await Assert.That(await host.Consumer.RejectAsync(message,
                new MessageRejectionReason(RejectionReason.DeliveryError))).IsTrue();
        }

        if (ownedProducers is "invalid" or "all")
        {
            var message = await host.ReceiveMessageAsync();
            await Assert.That(await host.Consumer.RejectAsync(message,
                new MessageRejectionReason(RejectionReason.Unacceptable))).IsTrue();
        }

        if (firstDisposeAsync)
            await host.DisposeAsync();
        else
            host.Dispose();

        // Act
        var exception = await TestExceptionRecorder.CaptureAsync(async () =>
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
        await Assert.That(exception).IsNull();
    }
}
