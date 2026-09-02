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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.Gcp.Tests.Helper;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Paramore.Brighter.Tasks;
using DeadLetterPolicy = Paramore.Brighter.MessagingGateway.GcpPubSub.DeadLetterPolicy;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway;

public class GcpPullMessageGatewayProvider
    : Pull.Proactor.IAmAMessageGatewayProactorProvider,
        Pull.Reactor.IAmAMessageGatewayReactorProvider
{
    private readonly GcpMessagingGatewayConnection _connection;
    private readonly GcpPubSubChannelFactory _channelFactory;
    private GcpPubSubSubscription? _lastSubscription;

    public GcpPullMessageGatewayProvider()
    {
        _connection = new GcpMessagingGatewayConnection
        {
            Credential = GatewayFactory.GetCredential(),
            ProjectId = GatewayFactory.GetProjectId(),
            PublisherConfiguration = cfg =>
            {
                cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
            },
            SubscriptionManagerConfiguration = cfg =>
            {
                cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
            },
        };
        _channelFactory = new GcpPubSubChannelFactory(_connection);
    }

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
    {
        return new RoutingKey($"gen-pull-{Guid.NewGuid():N}");
    }

    public ChannelName GetOrCreateChannelName([CallerMemberName] string? testName = null)
    {
        return new ChannelName($"gen-pull-{Guid.NewGuid():N}");
    }

    public GcpPublication CreatePublication(
        RoutingKey routingKey,
        OnMissingChannel makeChannels = OnMissingChannel.Create
    )
    {
        return new GcpPublication<MyCommand> { Topic = routingKey, MakeChannels = makeChannels };
    }

    public GcpPubSubSubscription CreateSubscription(
        RoutingKey routingKey,
        ChannelName channelName,
        OnMissingChannel makeChannel,
        bool setupDeadLetterQueue = false
    )
    {
        if (setupDeadLetterQueue)
        {
            var dlqTopic = $"dlq-pull-{Guid.NewGuid():N}";
            var dlqSub = $"dlq-pull-{Guid.NewGuid():N}";

            return new GcpPubSubSubscription<MyCommand>(
                subscriptionName: new SubscriptionName(channelName),
                channelName: channelName,
                routingKey: routingKey,
                messagePumpType: MessagePumpType.Proactor,
                ackDeadlineSeconds: 60,
                requeueCount: 5,
                deadLetter: new DeadLetterPolicy(new RoutingKey(dlqTopic), new ChannelName(dlqSub))
                {
                    AckDeadlineSeconds = 60,
                    MaxDeliveryAttempts = 5,
                },
                makeChannels: makeChannel,
                subscriptionMode: SubscriptionMode.Pull
            );
        }

        return new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(channelName),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: makeChannel,
            subscriptionMode: SubscriptionMode.Pull
        );
    }

    public IAmAMessageProducerSync CreateProducer(GcpPublication publication)
    {
        if (publication.MakeChannels == OnMissingChannel.Create)
        {
            BrighterAsyncContext.Run(() => _channelFactory.EnsureTopicExistAsync(
                new TopicAttributes { Name = publication.Topic!.Value, ProjectId = _connection.ProjectId },
                publication.MakeChannels));
        }

        var topicName = TopicName.FromProjectTopic(_connection.ProjectId, publication.Topic!.Value);
        var builder = new PublisherClientBuilder
        {
            Credential = _connection.Credential,
            TopicName = topicName,
            Settings = new PublisherClient.Settings
            {
                EnableMessageOrdering = publication.EnableMessageOrdering,
            },
        };
        _connection.PublisherConfiguration?.Invoke(builder);
        return new GcpMessageProducer(builder.Build(), publication);
    }

    public async Task<IAmAMessageProducerAsync> CreateProducerAsync(
        GcpPublication publication,
        CancellationToken cancellationToken = default
    )
    {
        if (publication.MakeChannels == OnMissingChannel.Create)
        {
            await _channelFactory.EnsureTopicExistAsync(
                new TopicAttributes { Name = publication.Topic!.Value, ProjectId = _connection.ProjectId },
                publication.MakeChannels);
        }

        var topicName = TopicName.FromProjectTopic(_connection.ProjectId, publication.Topic!.Value);
        var builder = new PublisherClientBuilder
        {
            Credential = _connection.Credential,
            TopicName = topicName,
            Settings = new PublisherClient.Settings
            {
                EnableMessageOrdering = publication.EnableMessageOrdering,
            },
        };
        _connection.PublisherConfiguration?.Invoke(builder);
        var client = await builder.BuildAsync(cancellationToken);
        return new GcpMessageProducer(client, publication);
    }

    public IAmAChannelSync CreateChannel(GcpPubSubSubscription subscription)
    {
        _lastSubscription = subscription;
        return _channelFactory.CreateSyncChannel(subscription);
    }

    public async Task<IAmAChannelAsync> CreateChannelAsync(
        GcpPubSubSubscription subscription,
        CancellationToken cancellationToken = default
    )
    {
        _lastSubscription = subscription;
        return await _channelFactory.CreateAsyncChannelAsync(subscription, cancellationToken);
    }

    public void CleanUp(
        IAmAMessageProducerSync? producer,
        IAmAChannelSync? channel,
        IEnumerable<Message> messages
    )
    {
        channel?.Dispose();
        producer?.Dispose();

        if (_lastSubscription != null)
        {
            _channelFactory.DeleteTopic(_lastSubscription);
            _channelFactory.DeleteSubscription(_lastSubscription);
        }
    }

    public async Task CleanUpAsync(
        IAmAMessageProducerAsync? producer,
        IAmAChannelAsync? channel,
        IEnumerable<Message> messages
    )
    {
        channel?.Dispose();

        if (producer != null)
        {
            await producer.DisposeAsync();
        }

        if (_lastSubscription != null)
        {
            await _channelFactory.DeleteTopicAsync(_lastSubscription);
            await _channelFactory.DeleteSubscriptionAsync(_lastSubscription);
        }
    }

    public async Task<Message> GetMessageFromDeadLetterQueueAsync(
        GcpPubSubSubscription subscription,
        CancellationToken cancellationToken = default
    )
    {
        // Create a subscription that reads from the DLQ subscription (already created by GCP)
        var dlqSubscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscription.DeadLetter!.Subscription!.Value),
            channelName: subscription.DeadLetter.Subscription,
            routingKey: subscription.DeadLetter.TopicName,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: OnMissingChannel.Assume,
            subscriptionMode: SubscriptionMode.Pull
        );

        var dlqChannel = await _channelFactory.CreateAsyncChannelAsync(
            dlqSubscription,
            cancellationToken
        );
        try
        {
            for (var i = 0; i < 10; i++)
            {
                var message = await dlqChannel.ReceiveAsync(
                    TimeSpan.FromSeconds(5),
                    cancellationToken
                );
                if (message.Header.MessageType != MessageType.MT_NONE)
                {
                    await dlqChannel.AcknowledgeAsync(message, cancellationToken);
                    return message;
                }

                await Task.Delay(1000, cancellationToken);
            }

            return new Message();
        }
        finally
        {
            dlqChannel.Dispose();
        }
    }

    public Message GetMessageFromDeadLetterQueue(GcpPubSubSubscription subscription)
    {
        // Create a subscription that reads from the DLQ subscription (already created by GCP)
        var dlqSubscription = new GcpPubSubSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(subscription.DeadLetter!.Subscription!.Value),
            channelName: subscription.DeadLetter.Subscription,
            routingKey: subscription.DeadLetter.TopicName,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Assume,
            subscriptionMode: SubscriptionMode.Pull
        );

        var dlqChannel = _channelFactory.CreateSyncChannel(dlqSubscription);
        try
        {
            for (var i = 0; i < 10; i++)
            {
                var message = dlqChannel.Receive(TimeSpan.FromSeconds(5));
                if (message.Header.MessageType != MessageType.MT_NONE)
                {
                    dlqChannel.Acknowledge(message);
                    return message;
                }

                Thread.Sleep(1000);
            }

            return new Message();
        }
        finally
        {
            dlqChannel.Dispose();
        }
    }
}
