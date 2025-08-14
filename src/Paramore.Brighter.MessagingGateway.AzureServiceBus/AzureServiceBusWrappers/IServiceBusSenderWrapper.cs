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
        /// <param name="cancellationToken">Cancellation Token.</param>
        Task SendAsync(ServiceBusMessage message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Send a Message
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        Task SendAsync(ServiceBusMessageBatch batch, CancellationToken cancellationToken = default);

        /// <summary>
        /// Schedule a message to be sent.
        /// </summary>
        /// <param name="message">Message to be scheduled.</param>
        /// <param name="scheduleEnqueueTime">The time to scheduled the message.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        Task ScheduleMessageAsync(ServiceBusMessage message, DateTimeOffset scheduleEnqueueTime,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///   Creates a size-constraint batch to which <see cref="ServiceBusMessage" /> may be added using a try-based pattern.  If a message would
        ///   exceed the maximum allowable size of the batch, the batch will not allow adding the message and signal that scenario using its
        ///   return value.
        ///
        ///   Because messages that would violate the size constraint cannot be added, publishing a batch will not trigger an exception when
        ///   attempting to send the messages to the Queue/Topic.
        /// </summary>
        ///
        /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> instance to signal the request to cancel the operation.</param>
        ValueTask<ServiceBusMessageBatch> CreateMessageBatchAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Close the Connection.
        /// </summary>
        Task CloseAsync();
    }
}
