using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.MessagingGateway;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.MessagingGateway.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

[Collection("MessagingGateway")]
public class MsSqlServerProactorTests : MessagingGatewayProactorTests<Publication, MsSqlSubscription>
{
    protected string TableName { get; } = $"Q{Uuid.New():N}";

    protected override async Task BeforeEachTestAsync(CancellationToken cancellationToken = default)
    {
        await Configuration.EnsureDatabaseExistsAsync(Configuration.DefaultConnectingString);
        await Configuration.CreateTableAsync(Configuration.DefaultConnectingString, MsSqlQueueBuilder.GetDDL(TableName));
        await Configuration.CreateTableAsync(Configuration.DefaultConnectingString, MsSqlQueueBuilder.GetIndexDDL(TableName));
    }

    protected override async Task CleanUpAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Configuration.DeleteTableAsync(Configuration.DefaultConnectingString, TableName);
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

    protected override async Task<IAmAMessageProducerAsync> CreateProducerAsync(Publication publication, CancellationToken cancellationToken = default)
    {
        var produces = await new MsSqlMessageProducerFactory(
                    new RelationalDatabaseConfiguration(Configuration.DefaultConnectingString, 
                        "brightertests",
                        queueStoreTable: publication.Topic!.Value),
                [publication])
            .CreateAsync();

        var producer = produces.First().Value;
        
        return (IAmAMessageProducerAsync)producer;
    }

    protected override async Task<IAmAChannelAsync> CreateChannelAsync(MsSqlSubscription subscription, CancellationToken cancellationToken = default)
    {
        var channel = await new ChannelFactory(new MsSqlMessageConsumerFactory (
                new RelationalDatabaseConfiguration(Configuration.DefaultConnectingString, 
                    "brightertests",
                    queueStoreTable: subscription.ChannelName.Value)))
            .CreateAsyncChannelAsync(subscription, cancellationToken);

        if (subscription.MakeChannels == OnMissingChannel.Create)
        {
            // Ensuring that the queue exists before return the channel
            await channel.ReceiveAsync(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
        
        return channel;
    }
}
