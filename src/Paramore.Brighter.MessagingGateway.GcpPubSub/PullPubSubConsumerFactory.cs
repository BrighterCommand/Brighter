namespace Paramore.Brighter.MessagingGateway.GcpPubSub;

/// <summary>
/// The Pull-based Pub/Sub consumer factory
/// </summary>
public class PullPubSubConsumerFactory(GcpMessagingGatewayConnection connection) : PubSubMessageGateway(connection), IAmAMessageConsumerFactory
{
    public IAmAMessageConsumerSync Create(Subscription subscription)
    {
        throw new NotImplementedException();
    }

    public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
    {
        throw new NotImplementedException();
    }
}
