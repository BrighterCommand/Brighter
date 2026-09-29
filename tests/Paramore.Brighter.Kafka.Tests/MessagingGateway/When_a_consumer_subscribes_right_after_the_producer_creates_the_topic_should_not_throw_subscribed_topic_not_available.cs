#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

/// <summary>
/// A freshly created topic can still be propagating when a consumer subscribes to it:
/// <see cref="KafkaMessageConsumer"/>'s constructor calls <c>Subscribe</c> before it runs its own
/// <c>EnsureTopic()</c>, and <see cref="KafkaMessagingGateway"/>'s <c>MakeTopic</c> returns as soon
/// as the broker accepts the create request, before the topic is necessarily visible in metadata.
/// This pins the race directly against the raw channel - unlike the generated gateway conformance
/// tests, which go through <see cref="RetryableChannelSync"/> and so tolerate exactly this failure.
/// </summary>
/// <remarks>
/// Attempts run concurrently, not in a plain sequential loop: on a fast, idle local broker, a
/// sequential loop never reproduced the race across 100+ attempts - the several broker round-trips
/// already inside <c>EnsureTopic</c>/<c>MakeTopic</c> happen to outlast the propagation window
/// before the next iteration's Subscribe. Concurrent attempts add the contention needed to widen
/// that window. RED was confirmed by throttling the local broker's CPU
/// (`docker update --cpus=0.25 kafka`) under this same concurrent load, which reproduced the exact
/// reported failure - `ConsumeException: Subscribed topic not available: &lt;topic&gt;: Broker:
/// Unknown topic or partition` - in a minority of attempts, matching the intermittent (not
/// constant) pattern seen across the four CI sightings in issue #4330. This test is committed
/// without that throttling: it is expected to rarely fail on a fast/idle CI runner and to catch the
/// regression on a loaded one. See bugfixes/0040-kafka-topic-propagation-race/bugfix.md for the
/// full diagnosis and the throttled-run evidence.
/// </remarks>
[Trait("Category", "Kafka")]
[Collection("Kafka")]
public class KafkaTopicPropagationRaceTests : IDisposable
{
    private const int Attempts = 60;
    private const int MaxConcurrency = 8;

    private readonly KafkaMessagingGatewayConfiguration _configuration = new()
    {
        Name = "Kafka Propagation Race Test",
        BootStrapServers = ["localhost:9092"],
    };

    private readonly ConcurrentBag<IAmAProducerRegistry> _registries = [];
    private readonly ConcurrentBag<IAmAChannelSync> _channels = [];
    private readonly ConcurrentBag<string> _topics = [];

    [Fact]
    public void When_a_consumer_subscribes_right_after_the_producer_creates_the_topic_should_not_throw_subscribed_topic_not_available()
    {
        // Arrange / Act - repeat, concurrently and many times, the sequence the generated gateway
        // tests use (producer creates the topic under OnMissingChannel.Create, consumer subscribes
        // immediately after), but read from the raw channel so a transient failure is not absorbed
        // by RetryableChannelSync the way it is in the generated conformance tests.
        var failures = new ConcurrentBag<ChannelFailureException>();

        Parallel.For(0, Attempts, new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrency }, _ =>
        {
            var topic = new RoutingKey($"gen.race.test.{Uuid.New():N}");
            _topics.Add(topic.Value);

            var producer = CreateProducer(topic);
            var channel = CreateRawChannel(topic);

            var message = new Message(
                new MessageHeader(Guid.NewGuid().ToString(), topic, MessageType.MT_EVENT),
                new MessageBody("propagation race probe"));

            producer.Send(message);

            try
            {
                var received = channel.Receive(TimeSpan.FromMilliseconds(5000));
                Assert.NotEqual(MessageType.MT_NONE, received.Header.MessageType);
            }
            catch (ChannelFailureException exception)
            {
                failures.Add(exception);
            }
        });

        // Assert - none of the raw receives should have hit the create-then-subscribe race
        var first = failures.FirstOrDefault();
        Assert.True(
            failures.Count == 0,
            $"{failures.Count}/{Attempts} attempts raced a just-created topic: {first?.Message} "
                + $"| inner: {first?.InnerException?.GetType().Name}: {first?.InnerException?.Message}");
    }

    private IAmAMessageProducerSync CreateProducer(RoutingKey topic)
    {
        var publication = new KafkaPublication
        {
            Topic = topic,
            NumPartitions = 1,
            ReplicationFactor = 1,
            MessageTimeoutMs = 2000,
            RequestTimeoutMs = 2000,
            MakeChannels = OnMissingChannel.Create,
        };

        var registry = new KafkaProducerRegistryFactory(_configuration, [publication]).Create();
        _registries.Add(registry);

        return (IAmAMessageProducerSync)registry.LookupBy(topic);
    }

    private IAmAChannelSync CreateRawChannel(RoutingKey topic)
    {
        var subscription = new KafkaSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.NewAsString()),
            channelName: new ChannelName($"Channel{Uuid.New():N}"),
            routingKey: topic,
            groupId: Guid.NewGuid().ToString(),
            offsetDefault: AutoOffsetReset.Earliest,
            commitBatchSize: 5,
            numOfPartitions: 1,
            replicationFactor: 1,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create);

        var channel = new ChannelFactory(new KafkaMessageConsumerFactory(_configuration))
            .CreateSyncChannel(subscription);
        _channels.Add(channel);

        return channel;
    }

    public void Dispose()
    {
        foreach (var channel in _channels)
        {
            try { channel.Dispose(); } catch { /* best-effort cleanup */ }
        }

        foreach (var registry in _registries)
        {
            try { registry.Dispose(); } catch { /* best-effort cleanup */ }
        }

        if (_topics.IsEmpty)
            return;

        try
        {
            using var adminClient = new AdminClientBuilder(
                new AdminClientConfig { BootstrapServers = "localhost:9092" }
            ).Build();

            adminClient.DeleteTopicsAsync(_topics).GetAwaiter().GetResult();
        }
        catch
        {
            // Best-effort cleanup; topic may not exist or broker may be unavailable
        }
    }
}
