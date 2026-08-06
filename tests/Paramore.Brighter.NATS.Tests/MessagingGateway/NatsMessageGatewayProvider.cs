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
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.Core;
using NATS.Client.JetStream;
using Paramore.Brighter.MessagingGateway.NATS;
using Paramore.Brighter.NATS.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.NATS.Tests.MessagingGateway;

/// <summary>
/// Messaging gateway provider for the generated core NATS test suite. Shares a single
/// <see cref="NatsConnection"/> between producer and consumer so that the subscription
/// is always registered with the server before any publish on the same subject.
/// </summary>
public class NatsMessageGatewayProvider
    : Proactor.IAmAMessageGatewayProactorProvider,
        Reactor.IAmAMessageGatewayReactorProvider
{
    private readonly NatsConnection _connection;
    private readonly NatsJSContext _jetStream;

    public NatsMessageGatewayProvider()
    {
        _connection = new NatsConnection();
        _jetStream = new NatsJSContext(_connection);
    }

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
    {
        return new RoutingKey($"gen.nats.{Guid.NewGuid().ToString("N")[..8]}");
    }

    public ChannelName GetOrCreateChannelName([CallerMemberName] string? testName = null)
    {
        return new ChannelName($"gen.nats.{Guid.NewGuid().ToString("N")[..8]}");
    }

    public NatsPublication CreatePublication(
        RoutingKey routingKey,
        OnMissingChannel makeChannels = OnMissingChannel.Create
    )
    {
        return new NatsPublication { Topic = routingKey, MakeChannels = makeChannels };
    }

    public NatsSubscription CreateSubscription(
        RoutingKey routingKey,
        ChannelName channelName,
        OnMissingChannel makeChannel,
        bool setupDeadLetterQueue = false
    )
    {
        // Core NATS has no queue infrastructure: the channel name is the subject the consumer
        // subscribes to, so it must match the routing key the producer publishes to.
        return new NatsSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Guid.NewGuid().ToString()),
            channelName: new ChannelName(routingKey.Value),
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: makeChannel
        );
    }

    public IAmAMessageProducerSync CreateProducer(NatsPublication publication)
    {
        return new NatsMessageProducer(_connection, publication, InstrumentationOptions.All);
    }

    public Task<IAmAMessageProducerAsync> CreateProducerAsync(
        NatsPublication publication,
        CancellationToken cancellationToken = default
    )
    {
        return Task.FromResult<IAmAMessageProducerAsync>(
            new NatsMessageProducer(_connection, publication, InstrumentationOptions.All)
        );
    }

    public IAmAChannelSync CreateChannel(NatsSubscription subscription)
    {
        return new NatsChannelFactory(new NatsMessageConsumerFactory(_connection, _jetStream))
            .CreateSyncChannel(subscription);
    }

    public Task<IAmAChannelAsync> CreateChannelAsync(
        NatsSubscription subscription,
        CancellationToken cancellationToken = default
    )
    {
        return new NatsChannelFactory(new NatsMessageConsumerFactory(_connection, _jetStream))
            .CreateAsyncChannelAsync(subscription, cancellationToken);
    }

    public Message GetMessageFromDeadLetterQueue(NatsSubscription subscription)
    {
        throw new NotSupportedException("Core NATS has no dead letter queue support");
    }

    public Task<Message> GetMessageFromDeadLetterQueueAsync(
        NatsSubscription subscription,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotSupportedException("Core NATS has no dead letter queue support");
    }

    public void CleanUp(
        IAmAMessageProducerSync? producer,
        IAmAChannelSync? channel,
        IEnumerable<Message> messages
    )
    {
        if (channel != null)
        {
            try { channel.Purge(); } catch { /* best effort */ }
            try { channel.Dispose(); } catch { /* best effort */ }
        }

        try { producer?.Dispose(); } catch { /* best effort */ }

        try
        {
            BrighterAsyncContext.Run(async () => await _connection.DisposeAsync());
        }
        catch { /* best effort */ }
    }

    public async Task CleanUpAsync(
        IAmAMessageProducerAsync? producer,
        IAmAChannelAsync? channel,
        IEnumerable<Message> messages
    )
    {
        if (channel != null)
        {
            try { await channel.PurgeAsync(); } catch { /* best effort */ }
            try { channel.Dispose(); } catch { /* best effort */ }
        }

        if (producer != null)
        {
            try { await producer.DisposeAsync(); } catch { /* best effort */ }
        }

        try { await _connection.DisposeAsync(); } catch { /* best effort */ }
    }
}
