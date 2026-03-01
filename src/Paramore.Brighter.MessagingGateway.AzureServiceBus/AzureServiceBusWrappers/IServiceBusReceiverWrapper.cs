using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    /// <summary>
    /// Interface for a wrapper over the Service Bus Receiver
    /// </summary>
    public interface IServiceBusReceiverWrapper
    {
        /// <summary>
        /// ReceiveAsync a batch of messages.
        /// </summary>
        /// <param name="batchSize">The size of the batch to receive.</param>
        /// <param name="serverWaitTime">Time to wait.</param>
        /// <returns></returns>
        Task<IEnumerable<IBrokeredMessageWrapper>> ReceiveAsync(int batchSize, TimeSpan serverWaitTime);

        /// <summary>
        /// CompleteAsync (Acknowledge) a Message.
        /// </summary>
        /// <param name="lockToken">The Lock Token the message was provider with.</param>
        Task CompleteAsync(string lockToken);

        /// <summary>
        /// Send a message to the Dead Letter Queue.
        /// </summary>
        /// <param name="lockToken">The Lock Token the message was provider with.</param>
        /// <returns></returns>
        Task DeadLetterAsync(string lockToken);

        /// <summary>
        /// Abandons a message, releasing the lock so the message is available for redelivery.
        /// </summary>
        /// <param name="lockToken">The Lock Token the message was provided with.</param>
        Task AbandonAsync(string lockToken);

        /// <summary>
        /// Close the connection.
        /// </summary>
        void Close();

        /// <summary>
        /// Closes the connection asynchronously.
        /// </summary>
        /// <returns></returns>
        Task CloseAsync();

        /// <summary>
        /// Is the connection currently closed.
        /// </summary>
        bool IsClosedOrClosing { get; }
    }
}
