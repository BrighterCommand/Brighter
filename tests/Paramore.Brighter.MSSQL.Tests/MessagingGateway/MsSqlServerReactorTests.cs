using System;
using System.Linq;
using Paramore.Brighter.Base.Test.MessagingGateway;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.MessagingGateway.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

public class MsSqlServerReactorTests : MessagingGatewayReactorTests<Publication, MsSqlSubscription>
{
    protected string TableName { get; } = $"Q{Uuid.New():N}";

    protected override void BeforeEachTest()
    {
        Configuration.EnsureDatabaseExists(Configuration.DefaultConnectingString);
        Configuration.CreateTable(Configuration.DefaultConnectingString, MsSqlQueueBuilder.GetDDL(TableName));
        Configuration.CreateTable(Configuration.DefaultConnectingString, MsSqlQueueBuilder.GetIndexDDL(TableName));
    }

    protected override void CleanUp()
    {
        try
        {
            Configuration.DeleteTable(Configuration.DefaultConnectingString, TableName);
        }
        catch 
        {
            // Ignoring any error
        }
    }

    protected override ChannelName GetOrCreateChannelName(string testName = null!)
    {
        return new ChannelName(TableName);
    }

    protected override RoutingKey GetOrCreateRoutingKey(string testName = null!)
    {
        return new RoutingKey(TableName);
    }
    
    protected override Publication CreatePublication(RoutingKey routingKey)
    {
        return new Publication<MyCommand>
        {
            Topic = routingKey,
            MakeChannels = OnMissingChannel.Create,
        };
    }

    protected override MsSqlSubscription CreateSubscription(RoutingKey routingKey, ChannelName channelName,
        OnMissingChannel makeChannel = OnMissingChannel.Create, bool setupDeadLetterQueue = false)
    {
        return new MsSqlSubscription <MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.NewAsString()),
            channelName: channelName,
            routingKey: routingKey,
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: makeChannel,
            bufferSize: 2);
    }

    protected override IAmAMessageProducerSync CreateProducer(Publication publication)
    {
        var produces = new MsSqlMessageProducerFactory(
                    new RelationalDatabaseConfiguration(Configuration.DefaultConnectingString, 
                        "brightertests",
                        queueStoreTable: publication.Topic!.Value),
                [publication])
            .Create();

        var producer = produces.First().Value;
        
        return (IAmAMessageProducerSync)producer;
    }

    protected override IAmAChannelSync CreateChannel(MsSqlSubscription subscription)
    {
        var channel = new ChannelFactory(new MsSqlMessageConsumerFactory (
                new RelationalDatabaseConfiguration(Configuration.DefaultConnectingString, 
                    "brightertests",
                    queueStoreTable: subscription.ChannelName.Value)))
            .CreateSyncChannel(subscription);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            // Ensuring that the queue exists before return the channel
            channel.Receive(TimeSpan.FromMilliseconds(100));
        }
        
        return channel;
    }
}
