﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Actions;
using Paramore.Brighter.Logging;
using Polly.CircuitBreaker;

namespace Paramore.Brighter.ServiceActivator
{
    // The message pump is a classic event loop and is intended to be run on a single-thread
    // The event loop is terminated when reading a MT_QUIT message on the channel
    // The event loop blocks on the Channel Listen call, though it will timeout
    // The event loop calls user code synchronously. You can post again for further decoupled invocation, but of course the likelihood is we are supporting decoupled invocation elsewhere
    // This is why you should spin up a thread for your message pump: to avoid blocking your main control path while you listen for a message and process it
    // It is also why throughput on a queue needs multiple performers, each with their own message pump
    // Retry and circuit breaker should be provided by exception policy using an attribute on the handler
    // Timeout on the handler should be provided by timeout policy using an attribute on the handler
    public abstract class MessagePump<TRequest> : IAmAMessagePump where TRequest : class, IRequest
    {
        internal static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MessagePump<TRequest>>();

        protected IAmACommandProcessor _commandProcessor;

        private readonly IAmAMessageMapper<TRequest> _messageMapper;
        private int _unacceptableMessageCount = 0;

        public MessagePump(
            IAmACommandProcessor commandProcessor, 
            IAmAMessageMapper<TRequest> messageMapper
            )
        {
            _commandProcessor = commandProcessor; 
            _messageMapper = messageMapper;
        }

        public int TimeoutInMilliseconds { get; set; }

        public int RequeueCount { get; set; }

        public int RequeueDelayInMilliseconds { get; set; }

        public int UnacceptableMessageLimit { get; set; }

        public IAmAChannel Channel { get; set; }

        public void Run()
        {
            do
            {
                if (UnacceptableMessageLimitReached())
                {
                    Channel.Dispose();
                    break;
                }

                s_logger.LogDebug("MessagePump: Receiving messages from channel {ChannelName} on thread # {ManagementThreadId}", Channel.Name, Thread.CurrentThread.ManagedThreadId);

                Message message = null;
                try
                {
                    message = Channel.Receive(TimeoutInMilliseconds);
                }
                catch (ChannelFailureException ex) when (ex.InnerException is BrokenCircuitException)
                {
                    s_logger.LogWarning("MessagePump: BrokenCircuitException messages from {ChannelName} on thread # {ManagementThreadId}", Channel.Name, Thread.CurrentThread.ManagedThreadId);
                    Task.Delay(1000).Wait();
                    continue;
                }
                catch (ChannelFailureException)
                {
                    s_logger.LogWarning("MessagePump: ChannelFailureException messages from {ChannelName} on thread # {ManagementThreadId}", Channel.Name, Thread.CurrentThread.ManagedThreadId);
                    Task.Delay(1000).Wait();
                    continue;
                }
                catch (Exception exception)
                {
                    s_logger.LogError(exception, "MessagePump: Exception receiving messages from {ChannelName} on thread # {ManagementThreadId}", Channel.Name, Thread.CurrentThread.ManagedThreadId);
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
                    s_logger.LogWarning("MessagePump: Failed to parse a message from the incoming message with id {Id} from {ChannelName} on thread # {ManagementThreadId}", message.Id, Channel.Name, Thread.CurrentThread.ManagedThreadId);

                    IncrementUnacceptableMessageLimit();
                    AcknowledgeMessage(message);

                    continue;
                }
 
                // QUIT command
                if (message.Header.MessageType == MessageType.MT_QUIT)
                {
                    s_logger.LogDebug("MessagePump: Quit receiving messages from {ChannelName} on thread # {ManagementThreadId}", Channel.Name, Thread.CurrentThread.ManagedThreadId);
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
                    s_logger.LogDebug(configurationException, "MessagePump: Stopping receiving of messages from {ChannelName} on thread # {ManagementThreadId}", Channel.Name, Thread.CurrentThread.ManagedThreadId);

                    RejectMessage(message);
                    Channel.Dispose();
                    break;
                }
                catch (DeferMessageAction)
                {
                    if (!RequeueMessage(message)) continue;
                }
                catch (AggregateException aggregateException)
                {
                    var (stop, requeue) = HandleProcessingException(aggregateException);

                    if (requeue)
                    {
                        if (!RequeueMessage(message)) continue;
                    }

                    if (stop)   
                    {
                        RejectMessage(message);
                        Channel.Dispose();
                        break;
                    }
                }
                catch (MessageMappingException messageMappingException)
                {
                    s_logger.LogWarning(messageMappingException,
                        "MessagePump: Failed to map message '{Id}' from {ChannelName} on thread # {ManagementThreadId}",
                        message.Id, Channel.Name, Thread.CurrentThread.ManagedThreadId);

                    IncrementUnacceptableMessageLimit();
                }
                catch (Exception e)
                {
                    s_logger.LogError(e,
                        "MessagePump: Failed to dispatch message '{Id}' from {ChannelName} on thread # {ManagementThreadId}",
                        message.Id, Channel.Name, Thread.CurrentThread.ManagedThreadId);
                }

                AcknowledgeMessage(message);

            } while (true);

            s_logger.LogDebug(
                "MessagePump0: Finished running message loop, no longer receiving messages from {ChannelName} on thread # {ManagementThreadId}",
                Channel.Name, Thread.CurrentThread.ManagedThreadId);

        }

        protected void AcknowledgeMessage(Message message)
        {
            s_logger.LogDebug(
                "MessagePump: Acknowledge message {Id} read from {ChannelName} on thread # {ManagementThreadId}",
                message.Id, Channel.Name, Thread.CurrentThread.ManagedThreadId);

            Channel.Acknowledge(message);
        }

        private bool DiscardRequeuedMessagesEnabled()
        {
            return RequeueCount != -1;
        }

        // Implemented in a derived class to dispatch to the relevant type of pipeline via the command processor
        // i..e an async pipeline uses SendAsync/PublishAsync and a blocking pipeline uses Send/Publish
        protected abstract void DispatchRequest(MessageHeader messageHeader, TRequest request);

        protected (bool, bool) HandleProcessingException(AggregateException aggregateException)
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
                    s_logger.LogDebug(exception,
                        "MessagePump: Stopping receiving of messages from {ChannelName} on thread # {ManagementThreadId}",
                        Channel.Name, Thread.CurrentThread.ManagedThreadId);
                    stop = true;
                    break;
                }

                s_logger.LogError(exception,
                    "MessagePump: Failed to dispatch message from {ChannelName} on thread # {ManagementThreadId}",
                    Channel.Name, Thread.CurrentThread.ManagedThreadId);
            }

            return (stop, requeue);
        }

        protected void IncrementUnacceptableMessageLimit()
        {
            _unacceptableMessageCount++;
        }

        protected void RejectMessage(Message message)
        {
            s_logger.LogDebug("MessagePump: Rejecting message {Id} from {ChannelName} on thread # {ManagementThreadId}", message.Id, Channel.Name, Thread.CurrentThread.ManagedThreadId);
            IncrementUnacceptableMessageLimit();

            Channel.Reject(message);
        }

        /// <summary>
        /// Requeue Message
        /// </summary>
        /// <param name="message">Message to be Requeued</param>
        /// <returns>Returns True if Message was Requeued, False if it was Rejected</returns>
        protected bool RequeueMessage(Message message)
        {
            message.UpdateHandledCount();

            if (DiscardRequeuedMessagesEnabled())
            {
                if (message.HandledCountReached(RequeueCount))
                {
                    var originalMessageId = message.Header.Bag.ContainsKey(Message.OriginalMessageIdHeaderName) ? message.Header.Bag[Message.OriginalMessageIdHeaderName].ToString() : null;

                    s_logger.LogError(
                        "MessagePump: Have tried {RequeueCount} times to handle this message {Id}{OriginalMessageId} from {ChannelName} on thread # {ManagementThreadId}, dropping message.{5}Message Body:{Request}",
                        RequeueCount,
                        message.Id,
                        string.IsNullOrEmpty(originalMessageId)
                            ? string.Empty
                            : $" (original message id {originalMessageId})",
                        Channel.Name,
                        Thread.CurrentThread.ManagedThreadId,
                        Environment.NewLine,
                        message.Body.Value);

                    RejectMessage(message);
                    return false;
                }
            }

            s_logger.LogDebug(
                "MessagePump: Re-queueing message {Id} from {ManagementThreadId} on thread # {ChannelName}", message.Id,
                Channel.Name, Thread.CurrentThread.ManagedThreadId);

            Channel.Requeue(message, RequeueDelayInMilliseconds);
            return true;
        }

        protected TRequest TranslateMessage(Message message)
        {
            if (_messageMapper == null)
            {
                throw new ConfigurationException($"No message mapper found for type {typeof(TRequest).FullName} for message {message.Id}.");
            }

            s_logger.LogDebug("MessagePump: Translate message {Id} on thread # {ManagementThreadId}", message.Id, Thread.CurrentThread.ManagedThreadId);

            TRequest request;

            try
            {
                request = _messageMapper.MapToRequest(message);
            }
            catch (Exception exception)
            {
                throw new MessageMappingException($"Failed to map message {message.Id} using message mapper {_messageMapper.GetType().FullName} for type {typeof(TRequest).FullName} ", exception);
            }

            return request;
        }

        protected bool UnacceptableMessageLimitReached()
        {
            if (UnacceptableMessageLimit == 0) return false;

            if (_unacceptableMessageCount >= UnacceptableMessageLimit)
            {
                s_logger.LogError(
                    "MessagePump: Unacceptable message limit of {UnacceptableMessageLimit} reached, stopping reading messages from {ChannelName} on thread # {ManagementThreadId}",
                    UnacceptableMessageLimit,
                    Channel.Name,
                    Thread.CurrentThread.ManagedThreadId
                );
                
                return true;
            }
            return false;
        }

        protected void ValidateMessageType(MessageType messageType, TRequest request)
        {
            if (messageType == MessageType.MT_COMMAND && request is IEvent)
            {
                throw new ConfigurationException(string.Format("Message {0} mismatch. Message type is '{1}' yet mapper produced message of type IEvent", request.Id,
                    MessageType.MT_COMMAND));
            }

            if (messageType == MessageType.MT_EVENT && request is ICommand)
            {
                throw new ConfigurationException(string.Format("Message {0} mismatch. Message type is '{1}' yet mapper produced message of type ICommand", request.Id,
                    MessageType.MT_EVENT));
            }
        }
   }
}
