using System;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public interface ITopicClient
    {
        void Send(Microsoft.Azure.ServiceBus.Message message);
        void ScheduleMessage(Microsoft.Azure.ServiceBus.Message message, DateTimeOffset scheduleEnqueueTime);
        void Close();

        Task SendAsync(Microsoft.Azure.ServiceBus.Message message);
        Task ScheduleMessageAsync(Microsoft.Azure.ServiceBus.Message message, DateTimeOffset scheduleEnqueueTime);
        Task CloseAsync();
    }
}
