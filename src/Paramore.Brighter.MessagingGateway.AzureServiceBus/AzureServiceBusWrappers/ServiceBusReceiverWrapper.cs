#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    /// <summary>
    /// Wraps the <see cref="ServiceBusReceiver"/> to provide additional functionality.
    /// </summary>
    internal sealed partial class ServiceBusReceiverWrapper : IServiceBusReceiverWrapper
    {
        private readonly ServiceBusReceiver _messageReceiver;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<ServiceBusReceiverWrapper>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceBusReceiverWrapper"/> class.
        /// </summary>
        /// <param name="messageReceiver">The <see cref="ServiceBusReceiver"/> to wrap.</param>
        public ServiceBusReceiverWrapper(ServiceBusReceiver messageReceiver)
        {
            _messageReceiver = messageReceiver;
        }

        /// <summary>
        /// Receives a batch of messages from the Service Bus.
        /// </summary>
        /// <param name="batchSize">The number of messages to receive.</param>
        /// <param name="serverWaitTime">The maximum time to wait for the messages.</param>
        /// <returns>A task that represents the asynchronous receive operation. The task result contains the received messages.</returns>
        public async Task<IEnumerable<IBrokeredMessageWrapper>> ReceiveAsync(int batchSize, TimeSpan serverWaitTime)
        {
            var messages = await _messageReceiver.ReceiveMessagesAsync(batchSize, serverWaitTime).ConfigureAwait(false);

            if (messages == null)
            {
                return new List<IBrokeredMessageWrapper>();
            }
            return messages.Select(x => new BrokeredMessageWrapper(x));
        }

        /// <summary>
        /// Closes the message receiver connection.
        /// </summary>
        public void Close()
        {
            Log.ClosingMessageReceiverConnection(s_logger);
            _messageReceiver.CloseAsync().GetAwaiter().GetResult();
            Log.MessageReceiverConnectionStopped(s_logger);
        }
        
        public async Task CloseAsync()
        {
            Log.ClosingMessageReceiverConnection(s_logger);
            await _messageReceiver.CloseAsync().ConfigureAwait(false);
            Log.MessageReceiverConnectionStopped(s_logger);
        }

        /// <summary>
        /// Completes the message processing.
        /// </summary>
        /// <param name="lockToken">The lock token of the message to complete.</param>
        /// <returns>A task that represents the asynchronous complete operation.</returns>
        public Task CompleteAsync(string lockToken)
        {
            return _messageReceiver.CompleteMessageAsync(CreateMessageShiv(lockToken));
        }

        /// <summary>
        /// Deadletters the message.
        /// </summary>
        /// <param name="lockToken">The lock token of the message to deadletter.</param>
        /// <returns>A task that represents the asynchronous deadletter operation.</returns>
        public Task DeadLetterAsync(string lockToken)
        {
            return _messageReceiver.DeadLetterMessageAsync(CreateMessageShiv(lockToken));
        }

        /// <summary>
        /// Abandons the message, releasing the lock so it is available for redelivery.
        /// </summary>
        /// <param name="lockToken">The lock token of the message to abandon.</param>
        /// <returns>A task that represents the asynchronous abandon operation.</returns>
        public Task AbandonAsync(string lockToken)
        {
            return _messageReceiver.AbandonMessageAsync(CreateMessageShiv(lockToken));
        }

        /// <summary>
        /// Gets a value indicating whether the message receiver is closed or closing.
        /// </summary>
        public bool IsClosedOrClosing => _messageReceiver.IsClosed;

        /// <summary>
        /// Creates a <see cref="ServiceBusReceivedMessage"/> with the specified lock token.
        /// </summary>
        /// <param name="lockToken">The lock token of the message.</param>
        /// <returns>A <see cref="ServiceBusReceivedMessage"/> with the specified lock token.</returns>
        private ServiceBusReceivedMessage CreateMessageShiv(string lockToken)
        {
            return ServiceBusModelFactory.ServiceBusReceivedMessage(lockTokenGuid: Guid.Parse(lockToken));
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Warning, "Closing the MessageReceiver connection")]
            public static partial void ClosingMessageReceiverConnection(ILogger logger);

            [LoggerMessage(LogLevel.Warning, "MessageReceiver connection stopped")]
            public static partial void MessageReceiverConnectionStopped(ILogger logger);
        }
    }
}

