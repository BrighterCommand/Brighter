using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.AzureServiceBus.Tests.Fakes;

public class FakeServiceBusReceiverProvider(IServiceBusReceiverWrapper receiver) : IServiceBusReceiverProvider
{
    public int CreationCount { get; private set; } = 0;
    public IServiceBusReceiverWrapper Get(string queueName, bool sessionEnabled)
    {
        CreationCount++;
        return receiver;
    }

    public IServiceBusReceiverWrapper Get(string topicName, string subscriptionName, bool sessionEnabled)
    {
        CreationCount++;
        return receiver;
    }
}
