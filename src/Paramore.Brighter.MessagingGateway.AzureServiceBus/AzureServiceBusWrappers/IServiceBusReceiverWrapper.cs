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
        /// Receive a batch of messages.
        /// </summary>
        /// <param name="batchSize">The size of the batch to receive.</param>
        /// <param name="serverWaitTime">Time to wait.</param>
        /// <returns></returns>
        Task<IEnumerable<IBrokeredMessageWrapper>> Receive(int batchSize, TimeSpan serverWaitTime);

        /// <summary>
        /// Complete (Acknowledge) a Message.
        /// </summary>
        /// <param name="lockToken">The Lock Token the message was provider with.</param>
        Task Complete(string lockToken);

        /// <summary>
        /// Send a message to the Dead Letter Queue.
        /// </summary>
        /// <param name="lockToken">The Lock Token the message was provider with.</param>
        /// <returns></returns>
        Task DeadLetter(string lockToken);

        /// <summary>
        /// Close the connection.
        /// </summary>
        void Close();

        /// <summary>
        /// Is the connection currently closed.
        /// </summary>
        bool IsClosedOrClosing { get; }
    }
}
