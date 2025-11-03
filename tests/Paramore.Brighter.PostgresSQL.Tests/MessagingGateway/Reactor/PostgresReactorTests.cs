using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.Base.Test.MessagingGateway;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.MessagingGateway.Postgres;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.MessagingGateway.Reactor;

[Collection("MessagingGateway")]
public class PostgresReactorTests  : MessagingGatewayReactorTests<PostgresPublication, PostgresSubscription>
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

    protected override IAmAMessageProducerSync CreateProducer(PostgresPublication publication)
    {
        var produces = new PostgresMessageProducerFactory(new PostgresMessagingGatewayConnection(
                new RelationalDatabaseConfiguration(Const.ConnectionString, 
                    "brightertests",
                    queueStoreTable: publication.Topic!.Value,
                    binaryMessagePayload: BinaryMessagePayload)),
                [publication])
            .Create();

        var producer = produces.First().Value;
        
        return (IAmAMessageProducerSync)producer;
    }

    protected override IAmAChannelSync CreateChannel(PostgresSubscription subscription)
    {
         var channel = new PostgresChannelFactory(new PostgresMessagingGatewayConnection(
                 new RelationalDatabaseConfiguration(Const.ConnectionString, 
                 "brightertests",
                 queueStoreTable: subscription.ChannelName.Value,
                 binaryMessagePayload: BinaryMessagePayload)))
            .CreateSyncChannel(subscription);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            // Ensuring that the queue exists before return the channel
            channel.Receive(TimeSpan.FromMilliseconds(100));
        }
        
        return channel;
    }

    protected override void CleanUp()
    {
        if (Subscription == null)
        {
            return;
        }

        try
        {
            using var connection = new NpgsqlConnection(Const.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE \"{Subscription.ChannelName}\"";
            command.ExecuteNonQuery();
        }
        catch
        {
            // Ignoring any future errors
        }
    }
}
