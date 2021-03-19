using System;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public interface ITopicClient
    {
        void Send(Microsoft.Azure.ServiceBus.Message message);
        void ScheduleMessage(Microsoft.Azure.ServiceBus.Message message, DateTimeOffset scheduleEnqueueTime);
        void Close();
    }
}
