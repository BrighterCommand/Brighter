using System;
using Common.Logging;

namespace paramore.brighter.commandprocessor.messaginggateway.restms
{
    public class RestMSServerRequestHandler : IAmAServerRequestHandler
    {
        public RestMSServerRequestHandler(ILog logger)
        {
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Receives the specified queue name.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <returns>Message.</returns>
        public Message Receive(string queueName, int timeoutInMilliseconds)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void Acknowledge(Message message)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="requeue">if set to <c>true</c> [requeue].</param>
        public void Reject(Message message, bool requeue)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Purges the specified queue name.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        public void Purge(string queueName)
        {
            throw new NotImplementedException();
        }
    }
}