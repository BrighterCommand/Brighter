using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.MessagingGateway;
using Paramore.Brighter.Base.Test.Requests;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureProactoTests : MessagingGatewayProactorTests<AzureServiceBusPublication, AzureServiceBusSubscription>
{
    protected override AzureServiceBusPublication CreatePublication(RoutingKey routingKey)
    {
        return new AzureServiceBusPublication<MyCommand>
        {
            Topic = routingKey,
            MakeChannels = OnMissingChannel.Create
        };
    }

    protected override AzureServiceBusSubscription CreateSubscription(RoutingKey routingKey, ChannelName channelName,
        OnMissingChannel makeChannel = OnMissingChannel.Create, bool setupDeadLetterQueue = false)
    {
        return new AzureServiceBusSubscription<MyCommand>(
            subscriptionName: new SubscriptionName(Uuid.NewAsString()),
            routingKey: routingKey,
            channelName: channelName,
            makeChannels: makeChannel,
            messagePumpType: MessagePumpType.Proactor);
    }

    protected override async Task<IAmAMessageProducerAsync> CreateProducerAsync(AzureServiceBusPublication publication, CancellationToken cancellationToken = default)
    {
        var clientProvider = ASBCreds.ASBClientProvider;
        var producers = await new AzureServiceBusMessageProducerFactory(
                clientProvider, 
                [publication],
                1)
            .CreateAsync();
        
        var producer = producers.First().Value;
        return (IAmAMessageProducerAsync)producer;
    }

    protected override async Task<IAmAChannelAsync> CreateChannelAsync(AzureServiceBusSubscription subscription, CancellationToken cancellationToken = default)
    {
        var clientProvider = ASBCreds.ASBClientProvider;
        var channel = await new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(clientProvider))
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
        var clientProvider = ASBCreds.ASBClientProvider;
        var administrationClient = new AdministrationClientWrapper(clientProvider);

        if (Publication != null)
        {
            try
            {
                await administrationClient.DeleteTopicAsync(Publication.Topic!.Value);
            }
            catch (Exception)
            {
                // Ignore any error during deleting topic
            }
        }

        if (Subscription != null)
        {
            try
            {
                await administrationClient.DeleteQueueAsync(Subscription.ChannelName.Value);
            }
            catch (Exception)
            {
                // Ignore any error during deleting topic
            }
        }
    }
}
