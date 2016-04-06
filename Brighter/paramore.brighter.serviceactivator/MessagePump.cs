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
using paramore.brighter.commandprocessor.actions;
using paramore.brighter.commandprocessor.Logging;

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
        private int _unacceptableMessageCount = 0;
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
        /// Gets or sets number of milliseconds to delay delivery of re-queued messages.
        /// </summary>
        public int RequeueDelayInMilliseconds { get; set; }
        /// <summary>
        /// Gets or Sets the unacceptable message limit, once the limit is reached the 
        /// </summary>
        public int UnacceptableMessageLimit { get; set; }
        /// <summary>
        /// Gets or sets the channel to read messages from.
        /// </summary>
        /// <value>The channel.</value>
        public IAmAChannel Channel { get; set; }
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
                if (UnacceptableMessageLimitReached())
                {
                    Channel.Dispose();
                    break;
                }
                
                if (Logger != null) Logger.DebugFormat("MessagePump: Receiving messages from channel {1} on thread # {0}", Thread.CurrentThread.ManagedThreadId, Channel.Name);

                Message message = null;
                try
                {
                    message = Channel.Receive(TimeoutInMilliseconds);
                }
                catch (ChannelFailureException)
                {
                    if (Logger != null) Logger.WarnFormat("MessagePump: ChannelFailureException messages from {1} on thread # {0}", Thread.CurrentThread.ManagedThreadId, Channel.Name);
                    continue;
                }
                catch (Exception exception)
                {
                    if (Logger != null) Logger.ErrorException("MessagePump: Exception receiving messages from {1} on thread # {0}", exception, Thread.CurrentThread.ManagedThreadId, Channel.Name);
                }

                if (message == null)
                {
                    Channel.Dispose();
                    throw new Exception("Could not receive message. Note that should return an MT_NONE from an empty queue on timeout");
                }

                // empty queue
                if (message.Header.MessageType == MessageType.MT_NONE)
                {
                    Task.Delay(500).Wait();
                    continue;
                }

                // failed to parse a message from the incoming data
                if (message.Header.MessageType == MessageType.MT_UNACCEPTABLE)
                {
                    if (Logger != null) Logger.WarnFormat("MessagePump: Failed to parse a message from the incoming message with id {1} from {2} on thread # {0}", Thread.CurrentThread.ManagedThreadId, message.Id, Channel.Name);

                    IncrementUnacceptableMessageLimit();
                    AcknowledgeMessage(message);

                    continue;
                }
 
                // QUIT command
                if (message.Header.MessageType == MessageType.MT_QUIT)
                {
                    if (Logger != null) Logger.DebugFormat("MessagePump: Quit receiving messages from {1} on thread # {0}", Thread.CurrentThread.ManagedThreadId, Channel.Name);
                    Channel.Dispose();
                    break;
                }

                // Serviceable message
                try
                {
                    var request = TranslateMessage(message);
                    DispatchRequest(message.Header, request);
                }
                catch (ConfigurationException configurationException)
                {
                    if (Logger != null) Logger.ErrorException("MessagePump: Stopping receiving of messages from {1} on thread # {0}", configurationException, Thread.CurrentThread.ManagedThreadId, Channel.Name);

                    RejectMessage(message);
                    Channel.Dispose();
                    break;
                }
                catch (DeferMessageAction)
                {
                    RequeueMessage(message);
                    continue;
                }
                catch (AggregateException aggregateException)
                {
                    var stopAndRequeue = HandleProcessingException(aggregateException);

                    if (stopAndRequeue.Item2)   //requeue
                    {
                        RequeueMessage(message);
                        continue;
                    }

                    if (stopAndRequeue.Item1)   //stop
                    {
                        RejectMessage(message);
                        Channel.Dispose();
                        break;
                    }
                }
                catch (MessageMappingException messageMappingException)
                {
                    if (Logger != null) Logger.WarnException("MessagePump: Failed to map the message from {1} on thread # {0}", messageMappingException, Thread.CurrentThread.ManagedThreadId, Channel.Name);

                    IncrementUnacceptableMessageLimit();
                }
                catch (Exception e)
                {
                    if (Logger != null) Logger.ErrorException("MessagePump: Failed to dispatch message from {1} on thread # {0}", e, Thread.CurrentThread.ManagedThreadId, Channel.Name);
                }

                AcknowledgeMessage(message);

            } while (true);

            if (Logger != null) Logger.DebugFormat("MessagePump: Finished running message loop, no longer receiving messages from {0} on thread # {1}", Channel.Name, Thread.CurrentThread.ManagedThreadId);
        }


        private void AcknowledgeMessage(Message message)
        {
            if (Logger != null) Logger.DebugFormat("MessagePump: Acknowledge message {0} read from {2} on thread # {1}", message.Id, Thread.CurrentThread.ManagedThreadId, Channel.Name);

            Channel.Acknowledge(message);
        }

        private bool DiscardRequeuedMessagesEnabled()
        {
            return RequeueCount != -1;
        }

        private void DispatchRequest(MessageHeader messageHeader, TRequest request)
        {
            if (Logger != null) Logger.DebugFormat("MessagePump: Dispatching message {0} from {2} on thread # {1}", request.Id, Thread.CurrentThread.ManagedThreadId, Channel.Name);

            if (messageHeader.MessageType == MessageType.MT_COMMAND && request is IEvent)
            {
                throw new ConfigurationException(string.Format("Message {0} mismatch. Message type is '{1}' yet mapper produced message of type IEvent", request.Id, MessageType.MT_COMMAND));
            }
            if (messageHeader.MessageType == MessageType.MT_EVENT && request is ICommand)
            {
                throw new ConfigurationException(string.Format("Message {0} mismatch. Message type is '{1}' yet mapper produced message of type ICommand", request.Id, MessageType.MT_EVENT));
            }

            switch (messageHeader.MessageType)
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

        private Tuple<bool, bool> HandleProcessingException(AggregateException aggregateException)
        {
            var stop = false;
            var requeue = false;
  
            foreach (var exception in aggregateException.InnerExceptions)
            {
                if (exception is DeferMessageAction)
                {
                    requeue = true;
                    continue;
                }

                if (exception is ConfigurationException)
                {
                    if (Logger != null) Logger.ErrorException("MessagePump: Stopping receiving of messages from {1} on thread # {0}", exception, Thread.CurrentThread.ManagedThreadId, Channel.Name);
                    stop = true;
                    break;
                }

                if (Logger != null) Logger.ErrorException("MessagePump: Failed to dispatch message from {1} on thread # {0}", exception, Thread.CurrentThread.ManagedThreadId, Channel.Name);
            }

            return new Tuple<bool, bool>(stop, requeue);
  
        }

        private void IncrementUnacceptableMessageLimit()
        {
            _unacceptableMessageCount++;
        }

        private void RejectMessage(Message message)
        {
            if (Logger != null) Logger.InfoFormat("MessagePump: Rejecting message {0} from {2} on thread # {1}", message.Id, Thread.CurrentThread.ManagedThreadId, Channel.Name);

            Channel.Reject(message);
        }

        private void RequeueMessage(Message message)
        {
            message.UpdateHandledCount();

            if (DiscardRequeuedMessagesEnabled())
            {
                if (message.HandledCountReached(RequeueCount))
                {
                    var originalMessageId = message.Header.Bag.ContainsKey(Message.OriginalMessageIdHeaderName) ? message.Header.Bag[Message.OriginalMessageIdHeaderName].ToString() : null;

                    if (Logger != null) 
                        Logger.ErrorFormat(
                            "MessagePump: Have tried {2} times to handle this message {0}{4} from {3} on thread # {1}, dropping message.{5}Message Body:{6}", 
                            message.Id, 
                            Thread.CurrentThread.ManagedThreadId, 
                            RequeueCount, 
                            Channel.Name,
                            string.IsNullOrEmpty(originalMessageId) ? string.Empty : string.Format(" (original message id {0})", originalMessageId),
                            Environment.NewLine,
                            message.Body.Value);

                    AcknowledgeMessage(message);
                    return;
                }
            }

            if (Logger != null) Logger.DebugFormat("MessagePump: Re-queueing message {0} from {2} on thread # {1}", message.Id, Thread.CurrentThread.ManagedThreadId, Channel.Name);

            Channel.Requeue(message, RequeueDelayInMilliseconds);
        }

        private TRequest TranslateMessage(Message message)
        {
            if (_messageMapper == null)
            {
                throw new ConfigurationException(string.Format("No message mapper found for type {0} for message {1}.", typeof(TRequest).FullName, message.Id));
            }

            if (Logger != null) Logger.DebugFormat("MessagePump: Translate message {0} on thread # {1}", message.Id, Thread.CurrentThread.ManagedThreadId);

            TRequest request;

            try
            {
                request = _messageMapper.MapToRequest(message);
            }
            catch (Exception exception)
            {
                throw new MessageMappingException(string.Format("Failed to map message {0} using message mapper {1} for type {2} ", message.Id, _messageMapper.GetType().FullName, typeof(TRequest).FullName), exception);
            }

            return request;
        }

        private bool UnacceptableMessageLimitReached()
        {
            if (UnacceptableMessageLimit == 0) return false;

            if (_unacceptableMessageCount >= UnacceptableMessageLimit)
            {
                if (Logger != null)
                {
                    Logger.ErrorFormat(
                        "MessagePump: Unacceptable message limit of {2} reached, stopping reading messages from {0} on thread # {1}",
                        Channel.Name,
                        Thread.CurrentThread.ManagedThreadId,
                        UnacceptableMessageLimit);
                }
                return true;
            }
            return false;
        }
    }

    internal class MessageMappingException : Exception
    {
        public MessageMappingException(string message, Exception exception) : base(message, exception) { }
    }
}