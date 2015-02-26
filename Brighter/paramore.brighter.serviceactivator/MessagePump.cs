// ***********************************************************************
// Assembly         : paramore.brighter.serviceactivator
// Author           : ian
// Created          : 01-26-2015
//
// Last Modified By : ian
// Last Modified On : 01-26-2015
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading;
using System.Threading.Tasks;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using ConfigurationException = paramore.brighter.commandprocessor.ConfigurationException;

namespace paramore.brighter.serviceactivator
{
    // The message pump is a classic event loop and is intended to be run on a single-thread
    // The event loop is terminated when reading a MT_QUIT message on the channel
    // The event loop blocks on the Channel Listen call, though it will timeout
    // The event loop calls user code synchronously. You can post again for further decoupled invocation, but of course the likelihood is we are supporting decoupled invocation elsewhere
    // This is why you should spin up a thread for your message pump: to avoid blocking your main control path while you listen for a message and process it
    // It is also why throughput on a queue needs multiple performers, each with their own message pump
    // Retry and circuit breaker should be provided by exception policy using an attribute on the handler
    // Timeout on the handler should be provided by timeout policy using an attribute on the handler
    /// <summary>
    /// Class MessagePump.
    /// </summary>
    /// <typeparam name="TRequest">The type of the t request.</typeparam>
    internal class MessagePump<TRequest> : IAmAMessagePump where TRequest : class, IRequest
    {
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly IAmAMessageMapper<TRequest> _messageMapper;
        /// <summary>
        /// Gets or sets the timeout in milliseconds, that the pump waits for a message on the queue before it yields control for an interval, prior to resuming.
        /// </summary>
        /// <value>The timeout in milliseconds.</value>
        public int TimeoutInMilliseconds { get; set; }
        /// <summary>
        /// Gets or sets the requeue count.
        /// </summary>
        /// <value>The requeue count.</value>
        public int RequeueCount { get; set; }
        /// <summary>
        /// Gets or sets the channel to read messages from.
        /// </summary>
        /// <value>The channel.</value>
        public IAmAnInputChannel Channel { get; set; }
        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        /// <value>The logger.</value>
        public ILog Logger { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagePump{TRequest}"/> class.
        /// </summary>
        /// <param name="commandProcessor">The command processor.</param>
        /// <param name="messageMapper">The message mapper.</param>
        public MessagePump(IAmACommandProcessor commandProcessor, IAmAMessageMapper<TRequest> messageMapper)
        {
            _commandProcessor = commandProcessor;
            _messageMapper = messageMapper;
        }

        /// <summary>
        /// Runs the message loop
        /// </summary>
        /// <exception cref="System.Exception">Could not receive message. Note that should return an MT_NONE from an empty queue on timeout</exception>
        public void Run()
        {
            do
            {
                if (Logger != null) Logger.DebugFormat("MessagePump: Receiving messages for {1} on thread # {0}", Thread.CurrentThread.ManagedThreadId, _messageMapper.GetType().ToString());
                Message message = null;
                try
                {
                    message = Channel.Receive(TimeoutInMilliseconds);
                }
                catch (ChannelFailureException)
                {
                    if (Logger != null) Logger.WarnFormat("MessagePump: ChannelFailureException messages for {1} on thread # {0}", Thread.CurrentThread.ManagedThreadId, _messageMapper.GetType().ToString());
                    continue;
                }
                catch (Exception exception)
                {
                    if (Logger != null)
                        Logger.ErrorFormat("MessagePump: Exception receiving messages for {1} on thread # {0} because of {2}", Thread.CurrentThread.ManagedThreadId, _messageMapper.GetType().ToString(), exception);
                }

                if (message == null) throw new Exception("Could not receive message. Note that should return an MT_NONE from an empty queue on timeout");

                // empty queue
                if (message.Header.MessageType == MessageType.MT_NONE)
                {
                    Task.Delay(500).Wait();
                    continue;
                }

                // failed to parse a message from the incoming data
                if (message.Header.MessageType == MessageType.MT_UNACCEPTABLE)
                {
                    if (Logger != null) Logger.WarnFormat("MessagePump: Failed to parse a message from the incoming message with id {1} for {2} on thread # {0}", Thread.CurrentThread.ManagedThreadId, message.Id, _messageMapper.GetType().ToString());
                    AcknowledgeMessage(message);
                    continue;
                }

                // QUIT command
                if (message.Header.MessageType == MessageType.MT_QUIT)
                {
                    if (Logger != null) Logger.DebugFormat("MessagePump: Quit receiving messages for {1} on thread # {0}", Thread.CurrentThread.ManagedThreadId, _messageMapper.GetType().ToString());
                    Channel.Dispose();
                    break;
                }

                // Serviceable message
                try
                {
                    DispatchRequest(message.Header.MessageType, TranslateMessage(message));
                }
                catch (ConfigurationException configurationException)
                {
                    if (Logger != null)
                        Logger.DebugFormat("MessagePump: {0} Stopping receiving of messages for {2} on thread # {1} because of {3}",
                                           configurationException.Message,
                                           Thread.CurrentThread.ManagedThreadId,
                                           _messageMapper.GetType().ToString(),
                                           configurationException);
                    break;
                }
                catch (RequeueException)
                {
                    RequeueMessage(message);
                }
                catch (AggregateException aggregateException)
                {
                    bool stop = false;
                    foreach (var exception in aggregateException.InnerExceptions)
                    {
                        if (exception is RequeueException)
                        {
                            RequeueMessage(message);
                            continue;
                        }
                        else if (exception is ConfigurationException)
                        {
                            if (Logger != null)
                                Logger.DebugFormat("MessagePump: {0} Stopping receiving of messages for {2} on thread # {1} because of {3}", exception.Message, Thread.CurrentThread.ManagedThreadId, _messageMapper.GetType().ToString(), exception);
                            stop = true;
                            break;
                        }
                    }

                    if (stop) break;
                }
                catch (Exception e)
                {
                    if (Logger != null)
                        Logger.ErrorFormat("MessagePump: Failed to dispatch message for {1} on thread # {0} because of {3}", Thread.CurrentThread.ManagedThreadId, _messageMapper.GetType().ToString(), e);
                }

                AcknowledgeMessage(message);
            } while (true);

            if (Logger != null)
                Logger.DebugFormat("MessagePump: Finished running message loop, no longer receiving messages for {0} on thread # {1}", _messageMapper.GetType().ToString(), Thread.CurrentThread.ManagedThreadId);
        }


        private void AcknowledgeMessage(Message message)
        {
            if (Logger != null)
                Logger.DebugFormat("MessagePump: Acknowledge message {0} on thread # {1}", message.Id, Thread.CurrentThread.ManagedThreadId);
            Channel.Acknowledge(message);
        }

        private bool DiscardRequeuedMessagesEnabled()
        {
            return RequeueCount != -1;
        }

        private void DispatchRequest(MessageType messageType, TRequest request)
        {
            if (Logger != null)
                Logger.DebugFormat("MessagePump: Dispatching message {0} on thread # {1}", request.Id, Thread.CurrentThread.ManagedThreadId);
            switch (messageType)
            {
                case MessageType.MT_COMMAND:
                    {
                        _commandProcessor.Send(request);
                        break;
                    }
                case MessageType.MT_DOCUMENT:
                case MessageType.MT_EVENT:
                    {
                        _commandProcessor.Publish(request);
                        break;
                    }
            }
        }

        private void RejectMessage(Message message)
        {
            if (Logger != null)
                Logger.DebugFormat("MessagePump: Rejecting message {0} on thread # {1}", message.Id, Thread.CurrentThread.ManagedThreadId);
            Channel.Reject(message);
        }

        private void RequeueMessage(Message message)
        {
            message.UpdateHandledCount();

            if (DiscardRequeuedMessagesEnabled())
            {
                if (message.HandledCountReached(RequeueCount))
                {
                    if (Logger != null)
                        Logger.WarnFormat("MessagePump: Have tried {2} times to handle this message {0} on thread # {1}, dropping message", message.Id, Thread.CurrentThread.ManagedThreadId, RequeueCount);

                    AcknowledgeMessage(message);
                    return;
                }
            }

            if (Logger != null)
                Logger.DebugFormat("MessagePump: Re-queueing message {0} on thread # {1}", message.Id, Thread.CurrentThread.ManagedThreadId);
            Channel.Requeue(message);
        }

        private TRequest TranslateMessage(Message message)
        {
            if (_messageMapper == null)
            {
                throw new ConfigurationException(string.Format("No message mapper found for type {0} for message {1}.", typeof(TRequest).FullName, message.Id));
            }

            if (Logger != null)
                Logger.DebugFormat("MessagePump: Translate message {0} on thread # {1}", message.Id, Thread.CurrentThread.ManagedThreadId);
            return _messageMapper.MapToRequest(message);
        }
    }
}