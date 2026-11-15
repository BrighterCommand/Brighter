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
using Confluent.Kafka;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

[Property("Category", "Kafka")]
[NotInParallel("Kafka")]
public class KafkaConsumerCleanupFailureTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(true, true)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    public async Task When_kafka_cleanup_fails_should_commit_pending_offsets(
        bool firstDisposeAsync, bool subsequentDisposeAsync)
    {
        // Arrange
        var timeProvider = new InMemoryFaultingTimerTimeProvider();
        await using var host = new KafkaDisposalTestHost(timeProvider);
        await Assert.That(await host.Consumer.RequeueAsync(await host.ReceiveMessageAsync())).IsTrue();
        await Assert.That(await host.Consumer.RejectAsync(await host.ReceiveMessageAsync(),
            new MessageRejectionReason(RejectionReason.DeliveryError))).IsTrue();
        var invalidMessage = await host.ReceiveMessageAsync();
        var pendingOffset = await Assert.That(invalidMessage.Header.Bag[HeaderNames.PARTITION_OFFSET]).IsTypeOf<TopicPartitionOffset>();
        await Assert.That(await host.Consumer.RejectAsync(invalidMessage,
            new MessageRejectionReason(RejectionReason.Unacceptable))).IsTrue();
        var timer = await Assert.That(timeProvider.Timer).IsTypeOf<InMemoryFaultingDisposalTimer>();
        using var observer = new ConsumerBuilder<string, byte[]>(new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = host.GroupId,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        }).Build();
        var before = await Assert.That(observer.Committed([pendingOffset.TopicPartition], TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(before.Offset).IsEqualTo(Offset.Unset);
        timer.FailOnDispose = true;

        // Act
        var exception = await TestExceptionRecorder.CaptureAsync(async () =>
        {
            if (firstDisposeAsync)
                await host.DisposeAsync();
            else
                host.Dispose();
        });

        // Assert
        await Assert.That(exception).IsSameReferenceAs(timer.Failure);
        var repeatedDisposal = await TestExceptionRecorder.CaptureAsync(async () =>
        {
            if (subsequentDisposeAsync)
                await host.Consumer.DisposeAsync();
            else
                host.Consumer.Dispose();
        });
        await Assert.That(repeatedDisposal).IsNull();
        await Assert.That(timer.DisposeCalls).IsEqualTo(1);

        var committed = await Assert.That(observer.Committed([pendingOffset.TopicPartition], TimeSpan.FromSeconds(5))).HasSingleItem();
        await Assert.That(committed.Offset).IsEqualTo(pendingOffset.Offset + 1);
    }
}
