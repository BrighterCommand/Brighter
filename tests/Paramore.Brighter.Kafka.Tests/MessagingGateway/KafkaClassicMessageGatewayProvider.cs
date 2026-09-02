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

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Paramore.Brighter.Kafka.Tests.MessagingGateway.Classic;
using Paramore.Brighter.Kafka.Tests.MessagingGateway.Classic.Proactor;
using Paramore.Brighter.Kafka.Tests.MessagingGateway.Classic.Reactor;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

public class KafkaClassicMessageGatewayProvider
    : IAmAMessageGatewayProactorProvider,
        IAmAMessageGatewayReactorProvider
{
    private readonly KafkaMessagingGatewayConfiguration _configuration = new()
    {
        Name = "Kafka Generated Test",
        BootStrapServers = ["localhost:9092"],
    };
    private readonly List<IAmAProducerRegistry> _producerRegistries = [];

    // Rejection-channel read hooks (FR-4/5/6/8/17): fresh Earliest consumers over the
    // subscription's DLQ / invalid-message topics, created lazily and reused across the
    // conformance test's bounded poll loop so offsets advance instead of re-reading from start.
    private IAmAMessageConsumerSync? _deadLetterConsumer;
    private IAmAMessageConsumerSync? _invalidChannelConsumer;
    private IAmAMessageConsumerAsync? _deadLetterConsumerAsync;
    private IAmAMessageConsumerAsync? _invalidChannelConsumerAsync;
    private readonly List<string> _rejectionTopics = [];

    // Delay hook (FR-2/9): Kafka has no native delayed delivery, so the gateway delegates a
    // requested delay to the producer's scheduler seam. The harness supplies a wall-clock scheduler
    // (shared across the producer and consumer paths) that re-publishes after the delay elapses.
    private KafkaHarnessMessageScheduler? _scheduler;

    private KafkaHarnessMessageScheduler Scheduler =>
        _scheduler ??= new KafkaHarnessMessageScheduler(_configuration);

    public void CleanUp(
        IAmAMessageProducerSync? producer,
        IAmAChannelSync? channel,
        IEnumerable<Message> messages
    )
    {
        var topics = CollectTopics(producer, channel);

        Dispose(channel);
        DisposeRegistries();
        Dispose(producer);
        Dispose(_deadLetterConsumer);
        Dispose(_invalidChannelConsumer);
        Dispose(_scheduler);

        topics.AddRange(_rejectionTopics);
        DeleteTopics(topics);

        static void Dispose(object? disposable)
        {
            try
            {
                if (disposable is IDisposable syncDisposable)
                {
                    syncDisposable.Dispose();
                }
            }
            catch
            {
                // Ignore any error during disposing
            }
        }
    }

    public async Task CleanUpAsync(
        IAmAMessageProducerAsync? producer,
        IAmAChannelAsync? channel,
        IEnumerable<Message> messages
    )
    {
        var topics = CollectTopics(producer, channel);

        await DisposeAsync(channel);
        DisposeRegistries();
        await DisposeAsync(producer);
        await DisposeAsync(_deadLetterConsumerAsync);
        await DisposeAsync(_invalidChannelConsumerAsync);
        await DisposeAsync(_scheduler);

        topics.AddRange(_rejectionTopics);
        DeleteTopics(topics);

        static async ValueTask DisposeAsync(object? disposable)
        {
            try
            {
                if (disposable is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (disposable is IDisposable syncDisposable)
                {
                    syncDisposable.Dispose();
                }
            }
            catch
            {
                // Ignore any error during disposing
            }
        }
    }

    public IAmAChannelSync CreateChannel(KafkaSubscription subscription)
    {
        var channel = new ChannelFactory(
            new KafkaMessageConsumerFactory(_configuration, Scheduler)
        ).CreateSyncChannel(subscription);

        return new RetryableChannelSync(channel);
    }

    public async Task<IAmAChannelAsync> CreateChannelAsync(
        KafkaSubscription subscription,
        CancellationToken cancellationToken = default
    )
    {
        var channel = await new ChannelFactory(
            new KafkaMessageConsumerFactory(_configuration, Scheduler)
        ).CreateAsyncChannelAsync(subscription, cancellationToken);

        return new RetryableChannelAsync(channel);
    }

    public IAmAMessageProducerSync CreateProducer(KafkaPublication publication)
    {
        var producerRegistry = new KafkaProducerRegistryFactory(
            _configuration,
            [publication]
        ).Create();

        _producerRegistries.Add(producerRegistry);

        var producer = (IAmAMessageProducerSync)producerRegistry.LookupBy(publication.Topic!);
        producer.Scheduler = Scheduler;
        return producer;
    }

    public async Task<IAmAMessageProducerAsync> CreateProducerAsync(
        KafkaPublication publication,
        CancellationToken cancellationToken = default
    )
    {
        var producerRegistry = await new KafkaProducerRegistryFactory(
            _configuration,
            [publication]
        ).CreateAsync(cancellationToken);

        _producerRegistries.Add(producerRegistry);

        var producer = (IAmAMessageProducerAsync)producerRegistry.LookupBy(publication.Topic!);
        producer.Scheduler = Scheduler;
        return producer;
    }

    public KafkaPublication CreatePublication(
        RoutingKey routingKey,
        OnMissingChannel makeChannels = OnMissingChannel.Create
    )
    {
        return new KafkaPublication
        {
            Topic = routingKey,
            NumPartitions = 1,
            ReplicationFactor = 1,
            MessageTimeoutMs = 2000,
            RequestTimeoutMs = 2000,
            MakeChannels = makeChannels,
        };
    }

    public KafkaSubscription CreateSubscription(
        RoutingKey routingKey,
        ChannelName channelName,
        OnMissingChannel makeChannel,
        RoutingKey? deadLetterRoutingKey = null,
        RoutingKey? invalidMessageRoutingKey = null
    )
    {
        return new KafkaSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.NewAsString()),
            channelName: channelName,
            routingKey: routingKey,
            groupId: Guid.NewGuid().ToString(),
            offsetDefault: AutoOffsetReset.Earliest,
            commitBatchSize: 5,
            numOfPartitions: 1,
            replicationFactor: 1,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: makeChannel,
            deadLetterRoutingKey: deadLetterRoutingKey,
            invalidMessageRoutingKey: invalidMessageRoutingKey
        );
    }

    public ChannelName GetOrCreateChannelName([CallerMemberName] string? testName = null)
    {
        return new ChannelName($"Channel{Uuid.New():N}");
    }

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
    {
        return new RoutingKey($"gen.test.{Uuid.New():N}");
    }

    public Task<Message> GetMessageFromDeadLetterQueueAsync(
        KafkaSubscription subscription,
        CancellationToken cancellationToken = default
    )
    {
        if (!TryGetRejectionTopic(subscription.DeadLetterRoutingKey, out var topic))
            return Task.FromResult(Message.Empty);

        _deadLetterConsumerAsync ??= CreateRejectionConsumerAsync(topic);
        return ReceiveOneAsync(_deadLetterConsumerAsync, cancellationToken);
    }

    public Message GetMessageFromDeadLetterQueue(KafkaSubscription subscription)
    {
        if (!TryGetRejectionTopic(subscription.DeadLetterRoutingKey, out var topic))
            return Message.Empty;

        _deadLetterConsumer ??= CreateRejectionConsumer(topic);
        return ReceiveOne(_deadLetterConsumer);
    }

    public Task<Message> GetMessageFromInvalidChannelAsync(
        KafkaSubscription subscription,
        CancellationToken cancellationToken = default
    )
    {
        if (!TryGetRejectionTopic(subscription.InvalidMessageRoutingKey, out var topic))
            return Task.FromResult(Message.Empty);

        _invalidChannelConsumerAsync ??= CreateRejectionConsumerAsync(topic);
        return ReceiveOneAsync(_invalidChannelConsumerAsync, cancellationToken);
    }

    public Message GetMessageFromInvalidChannel(KafkaSubscription subscription)
    {
        if (!TryGetRejectionTopic(subscription.InvalidMessageRoutingKey, out var topic))
            return Message.Empty;

        _invalidChannelConsumer ??= CreateRejectionConsumer(topic);
        return ReceiveOne(_invalidChannelConsumer);
    }

    private static bool TryGetRejectionTopic(RoutingKey? routingKey, out RoutingKey topic)
    {
        topic = routingKey ?? RoutingKey.Empty;
        return !RoutingKey.IsNullOrEmpty(routingKey);
    }

    private IAmAMessageConsumerSync CreateRejectionConsumer(RoutingKey topic)
    {
        _rejectionTopics.Add(topic.Value);
        return new KafkaMessageConsumerFactory(_configuration).Create(
            CreateRejectionSubscription(topic)
        );
    }

    private IAmAMessageConsumerAsync CreateRejectionConsumerAsync(RoutingKey topic)
    {
        _rejectionTopics.Add(topic.Value);
        return new KafkaMessageConsumerFactory(_configuration).CreateAsync(
            CreateRejectionSubscription(topic)
        );
    }

    private static KafkaSubscription CreateRejectionSubscription(RoutingKey topic)
    {
        return new KafkaSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.NewAsString()),
            channelName: new ChannelName($"Reader{Uuid.New():N}"),
            routingKey: topic,
            groupId: Guid.NewGuid().ToString(),
            offsetDefault: AutoOffsetReset.Earliest,
            commitBatchSize: 1,
            numOfPartitions: 1,
            replicationFactor: 1,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create
        );
    }

    private static Message ReceiveOne(IAmAMessageConsumerSync consumer)
    {
        try
        {
            var messages = consumer.Receive(TimeSpan.FromMilliseconds(500));
            return messages.Length > 0 ? messages[0] : Message.Empty;
        }
        catch (ChannelFailureException)
        {
            return Message.Empty;
        }
    }

    private static async Task<Message> ReceiveOneAsync(
        IAmAMessageConsumerAsync consumer,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var messages = await consumer.ReceiveAsync(
                TimeSpan.FromMilliseconds(500),
                cancellationToken
            );
            return messages.Length > 0 ? messages[0] : Message.Empty;
        }
        catch (ChannelFailureException)
        {
            return Message.Empty;
        }
    }

    public RejectionMetadataKeys RejectionMetadataKeys =>
        new RejectionMetadataKeys(
            HeaderNames.ORIGINAL_TOPIC,
            HeaderNames.ORIGINAL_TYPE,
            HeaderNames.REJECTION_REASON,
            HeaderNames.REJECTION_MESSAGE,
            HeaderNames.REJECTION_TIMESTAMP
        );

    private void DisposeRegistries()
    {
        foreach (var registry in _producerRegistries)
        {
            try
            {
                registry.Dispose();
            }
            catch
            {
                // Ignore any error during disposing
            }
        }
        _producerRegistries.Clear();
    }

    private static List<string> CollectTopics(object? producer, object? channel)
    {
        var topics = new HashSet<string>();

        if (
            producer is KafkaMessageProducer kafkaProducer
            && kafkaProducer.Publication is KafkaPublication publication
            && !RoutingKey.IsNullOrEmpty(publication.Topic)
        )
        {
            topics.Add(publication.Topic!.Value);
        }

        if (channel is IAmAChannel ch && ch.RoutingKey != RoutingKey.Empty)
        {
            topics.Add(ch.RoutingKey.Value);
        }

        return topics.ToList();
    }

    private void DeleteTopics(List<string> topics)
    {
        if (topics.Count == 0)
            return;

        try
        {
            using var adminClient = new AdminClientBuilder(
                new AdminClientConfig { BootstrapServers = "localhost:9092" }
            ).Build();

            adminClient.DeleteTopicsAsync(topics).GetAwaiter().GetResult();
        }
        catch
        {
            // Best-effort cleanup; topic may not exist or broker may be unavailable
        }
    }
}
