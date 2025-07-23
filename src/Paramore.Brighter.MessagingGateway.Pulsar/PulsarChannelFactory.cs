using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.Pulsar;

public class PulsarChannelFactory(PulsarMessagingGatewayConnection connection) : IAmAChannelFactory
{
    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
    {
        if (subscription is not PulsarSubscription publication)
        {
            throw new ConfigurationException("We expect PulsarSubscription or PulsarSubscription<T> as a parameter");
        }

        var client = connection.Create();
        
        throw new System.NotImplementedException();
    }

    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
    {
        throw new System.NotImplementedException();
    }

    public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default)
    {
        throw new System.NotImplementedException();
    }
}
