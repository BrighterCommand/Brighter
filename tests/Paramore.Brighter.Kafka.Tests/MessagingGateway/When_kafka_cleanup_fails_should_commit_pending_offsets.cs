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
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class KafkaConsumerCleanupFailureTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task When_kafka_cleanup_fails_should_commit_pending_offsets(
        bool firstDisposeAsync, bool subsequentDisposeAsync)
    {
        // Arrange
        var timeProvider = new InMemoryFaultingTimerTimeProvider();
        await using var host = new KafkaDisposalTestHost(timeProvider);
        Assert.True(await host.Consumer.RequeueAsync(await host.ReceiveMessageAsync()));
        Assert.True(await host.Consumer.RejectAsync(await host.ReceiveMessageAsync(),
            new MessageRejectionReason(RejectionReason.DeliveryError)));
        var invalidMessage = await host.ReceiveMessageAsync();
        var pendingOffset = Assert.IsType<TopicPartitionOffset>(invalidMessage.Header.Bag[HeaderNames.PARTITION_OFFSET]);
        Assert.True(await host.Consumer.RejectAsync(invalidMessage,
            new MessageRejectionReason(RejectionReason.Unacceptable)));
        var timer = Assert.IsType<InMemoryFaultingDisposalTimer>(timeProvider.Timer);
        using var observer = new ConsumerBuilder<string, byte[]>(new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = host.GroupId,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false
        }).Build();
        var before = Assert.Single(observer.Committed([pendingOffset.TopicPartition], TimeSpan.FromSeconds(5)));
        Assert.Equal(Offset.Unset, before.Offset);
        timer.FailOnDispose = true;

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            if (firstDisposeAsync)
                await host.DisposeAsync();
            else
                host.Dispose();
        });

        // Assert
        Assert.Same(timer.Failure, exception);
        var repeatedDisposal = await Record.ExceptionAsync(async () =>
        {
            if (subsequentDisposeAsync)
                await host.Consumer.DisposeAsync();
            else
                host.Consumer.Dispose();
        });
        Assert.Null(repeatedDisposal);
        Assert.Equal(1, timer.DisposeCalls);

        var committed = Assert.Single(observer.Committed([pendingOffset.TopicPartition], TimeSpan.FromSeconds(5)));
        Assert.Equal(pendingOffset.Offset + 1, committed.Offset);
    }
}
