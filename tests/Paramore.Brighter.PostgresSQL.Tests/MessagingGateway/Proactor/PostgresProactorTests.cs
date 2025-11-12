using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.Base.Test.MessagingGateway;
using Paramore.Brighter.Base.Test.MessagingGateway.Proactor;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.MessagingGateway.Postgres;

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway.Proactor;

public class PostgresProactorTests  : MessagingGatewayProactorTests<PostgresPublication, PostgresSubscription>
{
    protected string TableName { get; } = Uuid.New().ToString("N");
    protected virtual string Prefix => "Q";
    protected virtual bool BinaryMessagePayload => false;
    protected virtual bool LargePayload => false;
    protected override ChannelName GetOrCreateChannelName(string testName = null!)
    {
        return new ChannelName($"{Prefix}{TableName}");
    }

    protected override RoutingKey GetOrCreateRoutingKey(string testName = null!)
    {
        return new RoutingKey($"{Prefix}{TableName}");
    }

    protected override PostgresPublication CreatePublication(RoutingKey routingKey)
    {
        return new PostgresPublication<MyCommand>
        {
            Topic = routingKey,
            BinaryMessagePayload = BinaryMessagePayload,
            MakeChannels = OnMissingChannel.Create,
        };
    }

    protected override PostgresSubscription CreateSubscription(RoutingKey routingKey, ChannelName channelName,
        OnMissingChannel makeChannel = OnMissingChannel.Create, bool setupDeadLetterQueue = false)
    {
        return new PostgresSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.NewAsString()),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: makeChannel,
            bufferSize: 2,
            binaryMessagePayload: BinaryMessagePayload,
            tableWithLargeMessage: LargePayload);
    }

    protected override async Task<IAmAMessageProducerAsync> CreateProducerAsync(PostgresPublication publication, CancellationToken cancellationToken = default)
    {
        var produces = await new PostgresMessageProducerFactory(new PostgresMessagingGatewayConnection(
                new RelationalDatabaseConfiguration(Const.ConnectionString, 
                    "brightertests",
                    queueStoreTable: publication.Topic!.Value,
                    binaryMessagePayload: BinaryMessagePayload)),
                [publication])
            .CreateAsync();

        var producer = produces.First().Value;
        
        return (IAmAMessageProducerAsync)producer;
    }

    protected override async Task<IAmAChannelAsync> CreateChannelAsync(PostgresSubscription subscription, CancellationToken cancellationToken = default)
    {
         var channel = await new PostgresChannelFactory(new PostgresMessagingGatewayConnection(
                 new RelationalDatabaseConfiguration(Const.ConnectionString, 
                 "brightertests",
                 queueStoreTable: subscription.ChannelName.Value,
                 binaryMessagePayload: BinaryMessagePayload)))
            .CreateAsyncChannelAsync(subscription, cancellationToken);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            // Ensuring that the queue exists before return the channel
            await channel.ReceiveAsync(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
        
        return channel;
    }

    protected override async Task CleanUpAsync(CancellationToken cancellationToken = default)
    {
        if (Subscription == null)
        {
            return;
        }

        try
        {
            await using var connection = new NpgsqlConnection(Const.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE \"{Subscription.ChannelName}\"";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            // Ignoring any future errors
        }
    }
}
