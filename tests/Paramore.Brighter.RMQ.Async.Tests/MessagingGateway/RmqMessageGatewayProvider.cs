using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using Paramore.Brighter.RMQ.Async.Tests.TestDoubles;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway;

public class RmqMessageGatewayProvider : IAmAMessageGatewayProactorProvider
{
    private readonly RmqMessagingGatewayConnection _connection;

    public RmqMessageGatewayProvider()
    {
        _connection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange"),
        };
    }

    public async Task CleanUpAsync(IAmAMessageProducerAsync? producer, IAmAChannelAsync? channel)
    {
        if (channel != null)
        {
            await channel.PurgeAsync();
            channel.Dispose();
        }

        if (producer != null)
        {
            await producer.DisposeAsync();
        }
    }

    public async Task<IAmAChannelAsync> CreateChannelAsync(
        RmqSubscription subscription,
        CancellationToken cancellationToken = default
    )
    {
        var channel = await new ChannelFactory(
            new RmqMessageConsumerFactory(_connection)
        ).CreateAsyncChannelAsync(subscription, cancellationToken);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            // Ensuring that the queue exists before return the channel
            await channel.ReceiveAsync(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        return channel;
    }

    public async Task<IAmAMessageProducerAsync> CreateProducerAsync(
        RmqPublication publication,
        CancellationToken cancellationToken = default
    )
    {
        var produces = await new RmqMessageProducerFactory(
            _connection,
            [publication]
        ).CreateAsync();

        var producer = produces.First().Value;
        return (IAmAMessageProducerAsync)producer;
    }

    public RmqPublication CreatePublication(RoutingKey routingKey)
    {
        return new RmqPublication<MyCommand>
        {
            Topic = routingKey,
            MakeChannels = OnMissingChannel.Create,
        };
    }

    public RmqSubscription CreateSubscription(
        RoutingKey routingKey,
        ChannelName channelName,
        OnMissingChannel makeChannel
    )
    {
        return new RmqSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.NewAsString()),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: makeChannel
        );
    }

    public ChannelName GetOrCreateChannelName([CallerMemberName] string? testName = null)
    {
        return new ChannelName($"Queue{Uuid.New():N}");
    }

    public RoutingKey GetOrCreateRoutingKey([CallerMemberName] string? testName = null)
    {
        return new RoutingKey($"Topic{Uuid.New():N}");
    }
}
