using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public interface IMessageReceiverWrapper
    {
        Task<IEnumerable<IBrokeredMessageWrapper>> Receive(int batchSize, TimeSpan serverWaitTime);
        
        void Close();
        
        bool IsClosedOrClosing { get; }
    }
}
