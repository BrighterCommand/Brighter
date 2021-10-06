using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    public interface IServiceBusSenderWrapper
    {
        void Send(ServiceBusMessage message);

        void ScheduleMessage(ServiceBusMessage message, DateTimeOffset scheduleEnqueueTime);
        void Close();

        Task SendAsync(ServiceBusMessage message, CancellationToken cancellationToken = default(CancellationToken));

        Task ScheduleMessageAsync(ServiceBusMessage message, DateTimeOffset scheduleEnqueueTime,
            CancellationToken cancellationToken = default(CancellationToken));
        Task CloseAsync();
    }
}
