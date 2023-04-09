using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    /// <summary>
    /// Interface for the Wrapper over ServiceBusSender
    /// </summary>
    public interface IServiceBusSenderWrapper
    {
        /// <summary>
        /// Send a Message
        /// </summary>
        /// <param name="message">The message to send.</param>
        void Send(ServiceBusMessage message);

        /// <summary>
        /// Schedule a message to be sent.
        /// </summary>
        /// <param name="message">Message to be scheduled.</param>
        /// <param name="scheduleEnqueueTime">The time to scheduled the message.</param>
        void ScheduleMessage(ServiceBusMessage message, DateTimeOffset scheduleEnqueueTime);
        
        /// <summary>
        /// Close the Connection. 
        /// </summary>
        void Close();

        /// <summary>
        /// Send a Message
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        Task SendAsync(ServiceBusMessage message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send Messages
        /// </summary>
        /// <param name="messages">The messages to send.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        Task SendAsync(ServiceBusMessage[] messages, CancellationToken cancellationToken = default);

        /// <summary>
        /// Schedule a message to be sent.
        /// </summary>
        /// <param name="message">Message to be scheduled.</param>
        /// <param name="scheduleEnqueueTime">The time to scheduled the message.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <returns></returns>
        Task ScheduleMessageAsync(ServiceBusMessage message, DateTimeOffset scheduleEnqueueTime,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Close the Connection.
        /// </summary>
        /// <returns></returns>
        Task CloseAsync();
    }
}
