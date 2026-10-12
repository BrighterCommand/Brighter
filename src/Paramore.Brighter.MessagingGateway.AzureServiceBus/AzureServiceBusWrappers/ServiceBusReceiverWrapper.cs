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
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers
{
    /// <summary>
    /// Wraps the <see cref="ServiceBusReceiver"/> to provide additional functionality.
    /// </summary>
    internal sealed partial class ServiceBusReceiverWrapper : IAmAServiceBusRetryReceiver
    {
        private readonly ServiceBusReceiver _messageReceiver;
        private readonly TimeSpan _maxAutoLockRenewalDuration;
        private readonly object _gate = new();
        private readonly Dictionary<string, ServiceBusLock> _locks = new();
        private readonly HashSet<ServiceBusLock> _renewals = new();
        private ServiceBusLock? _sessionLock;
        private Task? _closing;
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<ServiceBusReceiverWrapper>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceBusReceiverWrapper"/> class.
        /// </summary>
        /// <param name="messageReceiver">The <see cref="ServiceBusReceiver"/> to wrap.</param>
        public ServiceBusReceiverWrapper(ServiceBusReceiver messageReceiver, TimeSpan maxAutoLockRenewalDuration)
        {
            _messageReceiver = messageReceiver;
            _maxAutoLockRenewalDuration = maxAutoLockRenewalDuration;
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
            lock (_gate)
            {
                if (_closing is not null) return Array.Empty<IBrokeredMessageWrapper>();
                foreach (var message in messages)
                    TrackLock(message);
            }
            return messages.Select(x => new BrokeredMessageWrapper(x));
        }

        /// <summary>
        /// Closes the message receiver connection.
        /// </summary>
        public void Close() => CloseAsync().GetAwaiter().GetResult();

        public Task CloseAsync()
        {
            TaskCompletionSource<bool> completion;
            ServiceBusLock[] locks;
            lock (_gate)
            {
                if (_closing is not null) return _closing;
                completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _closing = completion.Task;
                locks = _renewals.ToArray();
                _locks.Clear();
                _renewals.Clear();
                _sessionLock = null;
            }
            _ = CloseCoreAsync(locks, completion);
            return completion.Task;
        }

        private async Task CloseCoreAsync(ServiceBusLock[] locks, TaskCompletionSource<bool> completion)
        {
            try
            {
                Log.ClosingMessageReceiverConnection(s_logger);
                await Task.WhenAll(locks.Select(messageLock => messageLock.StopAsync())).ConfigureAwait(false);
                await _messageReceiver.CloseAsync().ConfigureAwait(false);
                Log.MessageReceiverConnectionStopped(s_logger);
                completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        public bool HasPendingMessages
        {
            get { lock (_gate) return _locks.Count > 0; }
        }

        public bool IsLockValid(string lockToken)
        {
            lock (_gate)
                return _locks.TryGetValue(lockToken, out var messageLock) && messageLock.IsValid;
        }

        public async Task ForgetAsync(string lockToken)
        {
            ServiceBusLock? messageLock;
            bool stopRenewal;
            lock (_gate)
            {
                if (!_locks.TryGetValue(lockToken, out messageLock)) return;
                _locks.Remove(lockToken);
                stopRenewal = _sessionLock is null || _locks.Count == 0;
                if (stopRenewal && ReferenceEquals(messageLock, _sessionLock))
                    _sessionLock = null;
            }
            if (stopRenewal)
            {
                await messageLock.StopAsync().ConfigureAwait(false);
                lock (_gate) _renewals.Remove(messageLock);
            }
        }

        private void TrackLock(ServiceBusReceivedMessage message)
        {
            if (_messageReceiver is ServiceBusSessionReceiver session)
            {
                _sessionLock ??= new ServiceBusLock(session.SessionLockedUntil, _maxAutoLockRenewalDuration,
                    session.EntityPath, "session", session.SessionId,
                    async cancellationToken =>
                    {
                        await session.RenewSessionLockAsync(cancellationToken).ConfigureAwait(false);
                        return session.SessionLockedUntil;
                    });
                _locks[message.LockToken] = _sessionLock;
            }
            else
            {
                _locks[message.LockToken] = new ServiceBusLock(message.LockedUntil, _maxAutoLockRenewalDuration,
                    _messageReceiver.EntityPath, "message", message.MessageId,
                    async cancellationToken =>
                    {
                        await _messageReceiver.RenewMessageLockAsync(message, cancellationToken).ConfigureAwait(false);
                        return message.LockedUntil;
                    });
            }
            _renewals.Add(_locks[message.LockToken]);
        }

        /// <summary>
        /// Completes the message processing.
        /// </summary>
        /// <param name="lockToken">The lock token of the message to complete.</param>
        /// <returns>A task that represents the asynchronous complete operation.</returns>
        public async Task CompleteAsync(string lockToken)
        {
            await ForgetAsync(lockToken).ConfigureAwait(false);
            await _messageReceiver.CompleteMessageAsync(CreateMessageShiv(lockToken)).ConfigureAwait(false);
        }

        /// <summary>
        /// Deadletters the message.
        /// </summary>
        /// <param name="lockToken">The lock token of the message to deadletter.</param>
        /// <returns>A task that represents the asynchronous deadletter operation.</returns>
        public Task DeadLetterAsync(string lockToken)
            => SettleDeadLetterAsync(lockToken, null, null);

        /// <summary>
        /// Deadletters the message, recording the reason and description in the broker's native
        /// dead-letter fields.
        /// </summary>
        /// <param name="lockToken">The lock token of the message to deadletter.</param>
        /// <param name="reason">The reason the message was dead-lettered. Truncated to 4096 characters as required by Azure Service Bus.</param>
        /// <param name="description">A fuller description of the dead-lettering. Truncated to 4096 characters as required by Azure Service Bus.</param>
        /// <returns>A task that represents the asynchronous deadletter operation.</returns>
        public Task DeadLetterAsync(string lockToken, string reason, string? description)
        {
            return SettleDeadLetterAsync(lockToken, Truncate(reason), description is null ? null : Truncate(description));
        }

        /// <summary>
        /// Abandons the message, releasing the lock so it is available for redelivery.
        /// </summary>
        /// <param name="lockToken">The lock token of the message to abandon.</param>
        /// <returns>A task that represents the asynchronous abandon operation.</returns>
        public async Task AbandonAsync(string lockToken)
        {
            await ForgetAsync(lockToken).ConfigureAwait(false);
            await _messageReceiver.AbandonMessageAsync(CreateMessageShiv(lockToken)).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task AbandonAsync(string lockToken, IDictionary<string, object> propertiesToModify,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ForgetAsync(lockToken).ConfigureAwait(false);
            await _messageReceiver.AbandonMessageAsync(CreateMessageShiv(lockToken), propertiesToModify,
                cancellationToken).ConfigureAwait(false);
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

        private async Task SettleDeadLetterAsync(string lockToken, string? reason, string? description)
        {
            await ForgetAsync(lockToken).ConfigureAwait(false);
            await _messageReceiver.DeadLetterMessageAsync(CreateMessageShiv(lockToken), reason, description).ConfigureAwait(false);
        }

        // Azure Service Bus rejects dead-letter reason/description values longer than 4096 characters
        // with an ArgumentOutOfRangeException, so clamp them to the limit.
        private const int MaxDeadLetterFieldLength = 4096;

        private static string Truncate(string value)
            => value.Length > MaxDeadLetterFieldLength ? value.Substring(0, MaxDeadLetterFieldLength) : value;

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Warning, "Closing the MessageReceiver connection")]
            public static partial void ClosingMessageReceiverConnection(ILogger logger);

            [LoggerMessage(LogLevel.Warning, "MessageReceiver connection stopped")]
            public static partial void MessageReceiverConnectionStopped(ILogger logger);
        }
    }
}
