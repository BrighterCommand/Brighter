using System;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

namespace Paramore.Brighter.AzureServiceBus.Tests.Fakes;

public class FakeServiceBusSenderProvider(IServiceBusSenderWrapper sender) : IServiceBusSenderProvider
{
    public int CreationCount { get; private set; } = 0;

    public Exception SingleThrowGetException { get; set; } = null;
    
    public IServiceBusSenderWrapper Get(string topicOrQueueName)
    {
        if (SingleThrowGetException != null)
        {
            var ex = SingleThrowGetException;
            SingleThrowGetException = null;
            throw ex;
        }
        CreationCount++;
        return sender;
    }
}
