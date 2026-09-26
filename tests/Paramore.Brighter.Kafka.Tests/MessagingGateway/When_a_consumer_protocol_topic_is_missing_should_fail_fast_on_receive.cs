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

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class KafkaMissingTopicDetectionTests : IAsyncLifetime
{
    private readonly RoutingKey _topic = new("missing-topic-" + Guid.NewGuid().ToString("N"));
    private readonly KafkaMessagingGatewayConfiguration _configuration = new()
    {
        Name = "Kafka missing topic detection",
        BootStrapServers = ["localhost:9092"]
    };
    private readonly IAdminClient _admin = new AdminClientBuilder(new AdminClientConfig
    {
        BootstrapServers = "localhost:9092",
        AllowAutoCreateTopics = false
    }).Build();
    private bool _creationAttempted;

    public Task InitializeAsync()
    {
        Assert.Equal(ErrorCode.UnknownTopicOrPart,
            Assert.Single(_admin.GetMetadata(_topic.Value, TimeSpan.FromSeconds(5)).Topics).Error.Code);
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_a_consumer_protocol_topic_is_missing_should_fail_fast_on_receive(
        bool useAsync, bool selectProtocolInHook)
    {
        //Arrange
        using var consumer = CreateConsumer(selectProtocolInHook: selectProtocolInHook);
        var elapsed = Stopwatch.StartNew();

        //Act
        var exception = await Record.ExceptionAsync(() => ReceiveAsync(consumer, useAsync, TimeSpan.Zero));

        //Assert
        AssertMissingTopic(exception);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(10),
            $"Missing-topic detection took {elapsed.Elapsed} with a five-second metadata timeout.");
        Assert.Equal(ErrorCode.UnknownTopicOrPart,
            Assert.Single(_admin.GetMetadata(_topic.Value, TimeSpan.FromSeconds(5)).Topics).Error.Code);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_an_existing_topic_is_empty_should_return_no_message_without_validating_its_shape(
        bool useAsync, bool useClassic)
    {
        //Arrange
        await CreateTopicAsync(partitions: 2);
        using var consumer = CreateConsumer(useClassic: useClassic, expectedPartitions: 3,
            expectedReplicationFactor: 2);

        //Act
        var messages = await ReceiveAsync(consumer, useAsync, TimeSpan.FromMilliseconds(250));
        var subsequentMessages = await ReceiveAsync(consumer, useAsync, TimeSpan.FromMilliseconds(250));

        //Assert
        Assert.Equal(MessageType.MT_NONE, Assert.Single(messages).Header.MessageType);
        Assert.Equal(MessageType.MT_NONE, Assert.Single(subsequentMessages).Header.MessageType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_a_missing_topic_is_created_later_should_receive_using_the_same_consumer(bool useAsync)
    {
        //Arrange
        using var consumer = CreateConsumer();
        var firstFailure = await Record.ExceptionAsync(() => ReceiveAsync(consumer, useAsync, TimeSpan.Zero));
        AssertMissingTopic(firstFailure);

        await CreateTopicAsync();
        using var producer = new KafkaMessageProducer(_configuration, new KafkaPublication
        {
            Topic = _topic,
            MakeChannels = OnMissingChannel.Assume,
            MessageTimeoutMs = 10000,
            RequestTimeoutMs = 5000
        });
        producer.Init();
        var message = new Message(
            new MessageHeader(Id.Random(), _topic, MessageType.MT_EVENT),
            new MessageBody("topic created after the first receive"));
        await producer.SendAsync(message);
        producer.Flush();

        //Act
        var received = await ReceiveMessageAsync(consumer, useAsync);

        //Assert
        Assert.Equal(message.Id, received.Id);
        Assert.Equal(message.Body.Value, received.Body.Value);
        consumer.Acknowledge(received);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_metadata_cannot_be_reached_should_report_a_bounded_failure_not_a_missing_topic(
        bool useAsync)
    {
        //Arrange
        using var consumer = CreateConsumer(configHook: config =>
        {
            config.BootstrapServers = "127.0.0.1:1";
            config.SocketTimeoutMs = 1000;
        }, metadataTimeout: TimeSpan.FromSeconds(1));
        var elapsed = Stopwatch.StartNew();

        //Act
        var exception = await Record.ExceptionAsync(() => ReceiveAsync(consumer, useAsync, TimeSpan.Zero));

        //Assert
        var failure = Assert.IsType<ChannelFailureException>(exception);
        var kafkaFailure = Assert.IsAssignableFrom<KafkaException>(failure.InnerException);
        Assert.NotEqual(ErrorCode.UnknownTopicOrPart, kafkaFailure.Error.Code);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(6),
            $"Metadata failure took {elapsed.Elapsed} with a one-second metadata timeout.");
    }

    private KafkaMessageConsumer CreateConsumer(bool useClassic = false, bool selectProtocolInHook = false,
        int expectedPartitions = 1, short expectedReplicationFactor = 1,
        Action<ConsumerConfig>? configHook = null, TimeSpan? metadataTimeout = null) =>
        new(_configuration, _topic, Guid.NewGuid().ToString("N"),
            makeChannels: OnMissingChannel.Assume,
            groupProtocol: useClassic || selectProtocolInHook
                ? new ClassicGroupProtocol()
                : new ConsumerGroupProtocol(),
            numPartitions: expectedPartitions,
            replicationFactor: expectedReplicationFactor,
            topicFindTimeout: metadataTimeout ?? TimeSpan.FromSeconds(5),
            readCommittedOffsetsTimeout: TimeSpan.FromSeconds(1),
            configHook: config =>
            {
                config.EnableMetricsPush = false;
                if (selectProtocolInHook)
                {
                    config.GroupProtocol = GroupProtocol.Consumer;
                    config.PartitionAssignmentStrategy = null;
                    config.SessionTimeoutMs = null;
                    config.HeartbeatIntervalMs = null;
                }

                configHook?.Invoke(config);
            });

    private void AssertMissingTopic(Exception? exception)
    {
        var failure = Assert.IsType<ChannelFailureException>(exception);
        var kafkaFailure = Assert.IsAssignableFrom<KafkaException>(failure.InnerException);
        Assert.Equal(ErrorCode.UnknownTopicOrPart, kafkaFailure.Error.Code);
        Assert.False(kafkaFailure.Error.IsFatal);
        Assert.Contains(_topic.Value, failure.ToString());
    }

    private async Task CreateTopicAsync(int partitions = 1)
    {
        _creationAttempted = true;
        await _admin.CreateTopicsAsync(
            [new TopicSpecification { Name = _topic.Value, NumPartitions = partitions, ReplicationFactor = 1 }],
            new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(10) });

        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(30))
        {
            var topic = _admin.GetMetadata(_topic.Value, TimeSpan.FromSeconds(2)).Topics.Single();
            if (topic.Error.Code == ErrorCode.NoError && topic.Partitions.Count == partitions
                && topic.Partitions.All(partition => partition.Leader >= 0 && partition.Error.Code == ErrorCode.NoError))
                return;

            await Task.Delay(100);
        }

        Assert.Fail("The test topic did not become ready within 30 seconds.");
    }

    private static async Task<Message[]> ReceiveAsync(KafkaMessageConsumer consumer, bool useAsync, TimeSpan timeout) =>
        useAsync ? await consumer.ReceiveAsync(timeout) : consumer.Receive(timeout);

    private static async Task<Message> ReceiveMessageAsync(KafkaMessageConsumer consumer, bool useAsync)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(30))
        {
            try
            {
                var message = Assert.Single(await ReceiveAsync(consumer, useAsync, TimeSpan.FromMilliseconds(500)));
                if (message.Header.MessageType != MessageType.MT_NONE)
                    return message;
            }
            catch (ChannelFailureException exception) when (
                exception.InnerException is KafkaException { Error.Code: ErrorCode.UnknownTopicOrPart })
            {
                // Topic metadata can lag behind successful creation while the consumer refreshes it.
            }

            await Task.Delay(100);
        }

        Assert.Fail("The existing consumer did not receive the message after topic creation within 30 seconds.");
        return new Message();
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (_creationAttempted)
            {
                try
                {
                    await _admin.DeleteTopicsAsync([_topic.Value],
                        new DeleteTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(10) });
                }
                catch (DeleteTopicsException exception) when (
                    exception.Results.All(result => result.Error.Code == ErrorCode.UnknownTopicOrPart))
                {
                    // A failed creation request may not have created the topic.
                }
            }
        }
        finally
        {
            _admin.Dispose();
        }
    }
}
