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
using Paramore.Brighter.Kafka.Tests.MessagingGateway.Consumer.Proactor;
using Paramore.Brighter.Kafka.Tests.MessagingGateway.Consumer.Reactor;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway;

public class KafkaConsumerMessageGatewayProvider
    : IAmAMessageGatewayProactorProvider,
        IAmAMessageGatewayReactorProvider
{
    private readonly KafkaMessagingGatewayConfiguration _configuration = new()
    {
        Name = "Kafka Generated Test",
        BootStrapServers = ["localhost:9092"],
    };
    private readonly List<IAmAProducerRegistry> _producerRegistries = [];

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
            new KafkaMessageConsumerFactory(_configuration)
        ).CreateSyncChannel(subscription);

        return new RetryableChannelSync(channel);
    }

    public async Task<IAmAChannelAsync> CreateChannelAsync(
        KafkaSubscription subscription,
        CancellationToken cancellationToken = default
    )
    {
        var channel = await new ChannelFactory(
            new KafkaMessageConsumerFactory(_configuration)
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

        return (IAmAMessageProducerSync)producerRegistry.LookupBy(publication.Topic!);
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

        return (IAmAMessageProducerAsync)producerRegistry.LookupBy(publication.Topic!);
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
        bool setupDeadLetterQueue = false
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
            makeChannels: makeChannel
        )
        {
            GroupProtocol = new ConsumerGroupProtocol()
        };
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
        throw new NotSupportedException(
            "Kafka does not support dead letter queues in generated tests."
        );
    }

    public Message GetMessageFromDeadLetterQueue(KafkaSubscription subscription)
    {
        throw new NotSupportedException(
            "Kafka does not support dead letter queues in generated tests."
        );
    }

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
