using System;
using System.Threading;
using System.Threading.Tasks;
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
    public class MessagePump<TRequest> : IAmAMessagePump where TRequest : class, IRequest
    {
        internal readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<MessagePump<TRequest>>);

        protected IAmACommandProcessor _commandProcessor;

        private readonly IAmAMessageMapper<TRequest> _messageMapper;
        private int _unacceptableMessageCount = 0;

        public MessagePump(IAmACommandProcessor commandProcessor, IAmAMessageMapper<TRequest> messageMapper)
        {
            _commandProcessor = commandProcessor;
            _messageMapper = messageMapper;
        }

        public int TimeoutInMilliseconds { get; set; }

        public int RequeueCount { get; set; }

        public int RequeueDelayInMilliseconds { get; set; }

        public int UnacceptableMessageLimit { get; set; }

        public IAmAChannel Channel { get; set; }

        public Task Run()
        {
            do
            {
                if (UnacceptableMessageLimitReached())
                {
                    Channel.Dispose();
                    break;
                }

                _logger.Value.DebugFormat("MessagePump: Receiving messages from channel {1} on thread # {0}", Thread.CurrentThread.ManagedThreadId, Channel.Name);

                Message message = null;
                try
                {
                    message = Channel.Receive(TimeoutInMilliseconds);
                }
                catch (ChannelFailureException ex) when (ex.InnerException is BrokenCircuitException)
                {
                    _logger.Value.WarnFormat("MessagePump: BrokenCircuitException messages from {1} on thread # {0}", Thread.CurrentThread.ManagedThreadId, Channel.Name);
                    Task.Delay(1000).Wait();
                    continue;
                }
                catch (ChannelFailureException)
                {
                    _logger.Value.WarnFormat("MessagePump: ChannelFailureException messages from {1} on thread # {0}", Thread.CurrentThread.ManagedThreadId, Channel.Name);
                    Task.Delay(1000).Wait();
                    continue;
                }
                catch (Exception exception)
                {
                    _logger.Value.ErrorException("MessagePump: Exception receiving messages from {1} on thread # {0}", exception, Thread.CurrentThread.ManagedThreadId, Channel.Name);
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
                    _logger.Value.WarnFormat("MessagePump: Failed to parse a message from the incoming message with id {1} from {2} on thread # {0}", Thread.CurrentThread.ManagedThreadId, message.Id, Channel.Name);

                    IncrementUnacceptableMessageLimit();
                    AcknowledgeMessage(message);

                    continue;
                }
 
                // QUIT command
                if (message.Header.MessageType == MessageType.MT_QUIT)
                {
                    _logger.Value.DebugFormat("MessagePump: Quit receiving messages from {1} on thread # {0}", Thread.CurrentThread.ManagedThreadId, Channel.Name);
                    Channel.Dispose();
                    break;
                }

                //async callback
                if (message.Header.MessageType == MessageType.MT_CALLBACK)
                {
                    message.Execute();
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
                    _logger.Value.DebugException("MessagePump: Stopping receiving of messages from {1} on thread # {0}", configurationException, Thread.CurrentThread.ManagedThreadId, Channel.Name);

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
                    var (stop, requeue) = HandleProcessingException(aggregateException);

                    if (requeue)   
                    {
                        RequeueMessage(message);
                        continue;
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
                    _logger.Value.WarnException("MessagePump: Failed to map message '{2}' from {1} on thread # {0}", messageMappingException, Thread.CurrentThread.ManagedThreadId, Channel.Name, message.Id);

                    IncrementUnacceptableMessageLimit();
                }
                catch (Exception e)
                {
                    _logger.Value.ErrorException("MessagePump: Failed to dispatch message '{2}' from {1} on thread # {0}", e, Thread.CurrentThread.ManagedThreadId, Channel.Name, message.Id);
                }

                AcknowledgeMessage(message);

            } while (true);

            _logger.Value.DebugFormat("MessagePump0: Finished running message loop, no longer receiving messages from {0} on thread # {1}", Channel.Name, Thread.CurrentThread.ManagedThreadId);
            
            return Task.CompletedTask;
        }

        protected void AcknowledgeMessage(Message message)
        {
            _logger.Value.DebugFormat("MessagePump: Acknowledge message {0} read from {2} on thread # {1}", message.Id, Thread.CurrentThread.ManagedThreadId, Channel.Name);

            Channel.Acknowledge(message);
        }

        private bool DiscardRequeuedMessagesEnabled()
        {
            return RequeueCount != -1;
        }
        
        protected void DispatchRequest(MessageHeader messageHeader, TRequest request)
        {
            _logger.Value.DebugFormat("MessagePump: Dispatching message {0} from {2} on thread # {1}", request.Id, Thread.CurrentThread.ManagedThreadId, Channel.Name);

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
                    _logger.Value.DebugException("MessagePump: Stopping receiving of messages from {1} on thread # {0}", exception, Thread.CurrentThread.ManagedThreadId, Channel.Name);
                    stop = true;
                    break;
                }

                _logger.Value.ErrorException("MessagePump: Failed to dispatch message from {1} on thread # {0}", exception, Thread.CurrentThread.ManagedThreadId, Channel.Name);
            }

            return (stop, requeue);
        }

        protected void IncrementUnacceptableMessageLimit()
        {
            _unacceptableMessageCount++;
        }

        protected void RejectMessage(Message message)
        {
            _logger.Value.DebugFormat("MessagePump: Rejecting message {0} from {2} on thread # {1}", message.Id, Thread.CurrentThread.ManagedThreadId, Channel.Name);

            Channel.Reject(message);
        }

        protected void RequeueMessage(Message message)
        {
            message.UpdateHandledCount();

            if (DiscardRequeuedMessagesEnabled())
            {
                if (message.HandledCountReached(RequeueCount))
                {
                    var originalMessageId = message.Header.Bag.ContainsKey(Message.OriginalMessageIdHeaderName) ? message.Header.Bag[Message.OriginalMessageIdHeaderName].ToString() : null;

                    _logger.Value.ErrorFormat(
                        "MessagePump: Have tried {2} times to handle this message {0}{4} from {3} on thread # {1}, dropping message.{5}Message Body:{6}", 
                        message.Id, 
                        Thread.CurrentThread.ManagedThreadId, 
                        RequeueCount, 
                        Channel.Name,
                        string.IsNullOrEmpty(originalMessageId) ? string.Empty : $" (original message id {originalMessageId})",
                        Environment.NewLine,
                        message.Body.Value);

                    AcknowledgeMessage(message);
                    return;
                }
            }

            _logger.Value.DebugFormat("MessagePump: Re-queueing message {0} from {2} on thread # {1}", message.Id, Thread.CurrentThread.ManagedThreadId, Channel.Name);

            Channel.Requeue(message, RequeueDelayInMilliseconds);
        }

        protected TRequest TranslateMessage(Message message)
        {
            if (_messageMapper == null)
            {
                throw new ConfigurationException($"No message mapper found for type {typeof(TRequest).FullName} for message {message.Id}.");
            }

            _logger.Value.DebugFormat("MessagePump: Translate message {0} on thread # {1}", message.Id, Thread.CurrentThread.ManagedThreadId);

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
                _logger.Value.ErrorFormat(
                    "MessagePump: Unacceptable message limit of {2} reached, stopping reading messages from {0} on thread # {1}",
                    Channel.Name,
                    Thread.CurrentThread.ManagedThreadId,
                    UnacceptableMessageLimit);
                
                return true;
            }
            return false;
        }

   }
}
