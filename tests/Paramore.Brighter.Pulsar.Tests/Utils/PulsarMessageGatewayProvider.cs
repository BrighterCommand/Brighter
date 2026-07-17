#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System.Runtime.CompilerServices;
using DotPulsar;
using Paramore.Brighter.MessagingGateway.Pulsar;
using Paramore.Brighter.Pulsar.Tests.MessagingGateway.Proactor;
using Paramore.Brighter.Pulsar.Tests.MessagingGateway.Reactor;
using Paramore.Brighter.Pulsar.Tests.TestDoubles;

namespace Paramore.Brighter.Pulsar.Tests.Utils;

public class PulsarMessageGatewayProvider
    : IAmAMessageGatewayProactorProvider,
        IAmAMessageGatewayReactorProvider
{
    private readonly PulsarMessagingGatewayConnection _connection;

    public PulsarMessageGatewayProvider()
    {
        _connection = GatewayFactory.CreateConnection();
    }

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
    {
        return new RoutingKey($"gen.pulsar.{testName}_{Guid.NewGuid():N}");
    }

    public ChannelName GetOrCreateChannelName([CallerMemberName] string? testName = null)
    {
        return new ChannelName($"Channel{testName}_{Guid.NewGuid():N}");
    }

    public PulsarPublication CreatePublication(RoutingKey routingKey, OnMissingChannel makeChannels = OnMissingChannel.Create)
    {
        return new PulsarPublication
        {
            Topic = routingKey,
            MakeChannels = makeChannels,
        };
    }

    public PulsarSubscription CreateSubscription(
        RoutingKey routingKey,
        ChannelName channelName,
        OnMissingChannel makeChannel,
        bool setupDeadLetterQueue = false)
    {
        return new PulsarSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Guid.NewGuid().ToString()),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: makeChannel,
            subscriptionType: SubscriptionType.Shared
        );
    }

    public IAmAMessageProducerSync CreateProducer(PulsarPublication publication)
    {
        var producers = new PulsarProducerFactory(_connection, [publication]).Create();
        return (IAmAMessageProducerSync)producers[new ProducerKey(publication.Topic!, publication.Type)];
    }

    public async Task<IAmAMessageProducerAsync> CreateProducerAsync(
        PulsarPublication publication,
        CancellationToken cancellationToken = default)
    {
        var producers = await new PulsarProducerFactory(_connection, [publication]).CreateAsync();
        return (IAmAMessageProducerAsync)producers[new ProducerKey(publication.Topic!, publication.Type)];
    }

    public IAmAChannelSync CreateChannel(PulsarSubscription subscription)
    {
        var channelFactory = new PulsarChannelFactory(new PulsarMessageConsumerFactory(_connection));
        return channelFactory.CreateSyncChannel(subscription);
    }

    public async Task<IAmAChannelAsync> CreateChannelAsync(
        PulsarSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        var channelFactory = new PulsarChannelFactory(new PulsarMessageConsumerFactory(_connection));
        return await channelFactory.CreateAsyncChannelAsync(subscription, cancellationToken);
    }

    public void CleanUp(
        IAmAMessageProducerSync? producer,
        IAmAChannelSync? channel,
        IEnumerable<Message> messages)
    {
        channel?.Dispose();
        producer?.Dispose();
    }

    public async Task CleanUpAsync(
        IAmAMessageProducerAsync? producer,
        IAmAChannelAsync? channel,
        IEnumerable<Message> messages)
    {
        channel?.Dispose();

        if (producer != null)
        {
            await producer.DisposeAsync();
        }
    }

    public Message GetMessageFromDeadLetterQueue(PulsarSubscription subscription)
    {
        throw new NotSupportedException("Pulsar does not support dead letter queues in generated tests.");
    }

    public Task<Message> GetMessageFromDeadLetterQueueAsync(
        PulsarSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Pulsar does not support dead letter queues in generated tests.");
    }
}
