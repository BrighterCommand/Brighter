using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.AzureServiceBus.Tests.Fakes;

public class FakeServiceBusReceiverProvider(IServiceBusReceiverWrapper? receiver) : IServiceBusReceiverProvider
{
    public int CreationCount { get; private set; } = 0;
    public Task<IServiceBusReceiverWrapper?> GetAsync(string queueName, bool sessionEnabled)
    {
        CreationCount++;
        return Task.FromResult(receiver);
    }

    public Task<IServiceBusReceiverWrapper?> GetAsync(string topicName, string subscriptionName, bool sessionEnabled)
    {
        CreationCount++;
        return Task.FromResult(receiver);
    }
}
