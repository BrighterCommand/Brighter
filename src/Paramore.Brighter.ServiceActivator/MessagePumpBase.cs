using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Actions;
using Paramore.Brighter.ServiceActivator.Logging;
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
    public abstract class MessagePumpBase<TRequest> where TRequest : class, IRequest
    {
        protected static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<MessagePumpBase<TRequest>>);

        protected IAmAChannel _channel;
        protected IAmACommandProcessor _commandProcessor;

        private readonly IAmAMessageMapper<TRequest> _messageMapper;
        private int _unacceptableMessageCount = 0;

        protected MessagePumpBase(IAmAChannel channel, IAmACommandProcessor commandProcessor, IAmAMessageMapper<TRequest> messageMapper)
        {
            _channel = channel;
            _commandProcessor = commandProcessor;
            _messageMapper = messageMapper;
        }

        public int TimeoutInMilliseconds { get; set; }

        public int RequeueCount { get; set; }

        public int RequeueDelayInMilliseconds { get; set; }

        public int UnacceptableMessageLimit { get; set; }

        protected abstract Task DispatchRequest(MessageHeader messageHeader, TRequest request, CancellationToken cancellationToken);

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (UnacceptableMessageLimitReached())
                {
                    _channel.Dispose();
                    break;
                }

                _logger.Value.DebugFormat("MessagePump: Receiving messages from channel {1} on thread # {0}", Thread.CurrentThread.ManagedThreadId, _channel.Name);

                Message message = null;
                try
                {
                    message = await _channel.ReceiveAsync(TimeoutInMilliseconds);
                }
                catch (ChannelFailureException ex) when (ex.InnerException is BrokenCircuitException)
                {
                    _logger.Value.WarnFormat("MessagePump: BrokenCircuitException messages from {1} on thread # {0}", Thread.CurrentThread.ManagedThreadId, _channel.Name);
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }
                catch (ChannelFailureException)
                {
                    _logger.Value.WarnFormat("MessagePump: ChannelFailureException messages from {1} on thread # {0}", Thread.CurrentThread.ManagedThreadId, _channel.Name);
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }
                catch (Exception exception)
                {
                    _logger.Value.ErrorException("MessagePump: Exception receiving messages from {1} on thread # {0}", exception, Thread.CurrentThread.ManagedThreadId, _channel.Name);
                }

                if (message == null)
                {
                    _channel.Dispose();
                    throw new Exception("Could not receive message. Note that should return an MT_NONE from an empty queue on timeout");
                }

                // empty queue
                if (message.Header.MessageType == MessageType.MT_NONE)
                {
                    await Task.Delay(500, cancellationToken);
                    continue;
                }

                // failed to parse a message from the incoming data
                if (message.Header.MessageType == MessageType.MT_UNACCEPTABLE)
                {
                    _logger.Value.WarnFormat("MessagePump: Failed to parse a message from the incoming message with id {1} from {2} on thread # {0}", Thread.CurrentThread.ManagedThreadId, message.Id, _channel.Name);

                    IncrementUnacceptableMessageLimit();
                    await AcknowledgeMessageAsync(message);

                    continue;
                }
 
                // QUIT command
                if (message.Header.MessageType == MessageType.MT_QUIT)
                {
                    _logger.Value.DebugFormat("MessagePump: Quit receiving messages from {1} on thread # {0}", Thread.CurrentThread.ManagedThreadId, _channel.Name);
                    _channel.Dispose();
                    break;
                }

                //ayn ca
                if (message.Header.MessageType == MessageType.MT_CALLBACK)
                {
                    message.Execute();
                    break;
                }

                // Serviceable message
                try
                {
                    var request = TranslateMessage(message);
                    await DispatchRequest(message.Header, request, cancellationToken);
                }
                catch (ConfigurationException configurationException)
                {
                    _logger.Value.DebugException("MessagePump: Stopping receiving of messages from {1} on thread # {0}", configurationException, Thread.CurrentThread.ManagedThreadId, _channel.Name);

                    await RejectMessageAsync(message);
                    _channel.Dispose();
                    break;
                }
                catch (DeferMessageAction)
                {
                    await RequeueMessageAsync(message);
                    continue;
                }
                catch (AggregateException aggregateException)
                {
                    (var stop, var requeue) = HandleProcessingException(aggregateException);

                    if (requeue)
                    {
                        await RequeueMessageAsync(message);
                        continue;
                    }

                    if (stop)
                    {
                        await RejectMessageAsync(message);
                        _channel.Dispose();
                        break;
                    }
                }
                catch (MessageMappingException messageMappingException)
                {
                    _logger.Value.WarnException("MessagePump: Failed to map the message from {1} on thread # {0}", messageMappingException, Thread.CurrentThread.ManagedThreadId, _channel.Name);

                    IncrementUnacceptableMessageLimit();
                }
                catch (Exception e)
                {
                    _logger.Value.ErrorException("MessagePump: Failed to dispatch message from {1} on thread # {0}", e, Thread.CurrentThread.ManagedThreadId, _channel.Name);
                }

                await AcknowledgeMessageAsync(message);
            }

            _logger.Value.DebugFormat("MessagePump: Finished running message loop, no longer receiving messages from {0} on thread # {1}", _channel.Name, Thread.CurrentThread.ManagedThreadId);
        }

        private async Task AcknowledgeMessageAsync(Message message)
        {
            _logger.Value.DebugFormat("MessagePump: Acknowledge message {0} read from {2} on thread # {1}", message.Id, Thread.CurrentThread.ManagedThreadId, _channel.Name);

            await _channel.AcknowledgeAsync(message);
        }

        private bool DiscardRequeuedMessagesEnabled()
        {
            return RequeueCount != -1;
        }

        private (bool stop, bool requeue) HandleProcessingException(AggregateException aggregateException)
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
                    _logger.Value.DebugException("MessagePump: Stopping receiving of messages from {1} on thread # {0}", exception, Thread.CurrentThread.ManagedThreadId, _channel.Name);
                    stop = true;
                    break;
                }

                _logger.Value.ErrorException("MessagePump: Failed to dispatch message from {1} on thread # {0}", exception, Thread.CurrentThread.ManagedThreadId, _channel.Name);
            }

            return (stop, requeue);
        }

        private void IncrementUnacceptableMessageLimit()
        {
            Interlocked.Increment(ref _unacceptableMessageCount);
        }

        private async Task RejectMessageAsync(Message message)
        {
            _logger.Value.DebugFormat("MessagePump: Rejecting message {0} from {2} on thread # {1}", message.Id, Thread.CurrentThread.ManagedThreadId, _channel.Name);

            await _channel.RejectAsync(message);
        }

        private async Task RequeueMessageAsync(Message message)
        {
            message.UpdateHandledCount();

            if (DiscardRequeuedMessagesEnabled() && message.HandledCountReached(RequeueCount))
            {
                var originalMessageId = message.Header.Bag.ContainsKey(Message.OriginalMessageIdHeaderName) ? message.Header.Bag[Message.OriginalMessageIdHeaderName].ToString() : null;

                _logger.Value.ErrorFormat(
                    "MessagePump: Have tried {2} times to handle this message {0}{4} from {3} on thread # {1}, dropping message.{5}Message Body:{6}",
                    message.Id,
                    Thread.CurrentThread.ManagedThreadId, 
                    RequeueCount,
                    _channel.Name,
                    string.IsNullOrEmpty(originalMessageId) ? string.Empty : string.Format(" (original message id {0})", originalMessageId),
                    Environment.NewLine,
                    message.Body.Value);

                await AcknowledgeMessageAsync(message);
                return;
            }

            _logger.Value.DebugFormat("MessagePump: Re-queueing message {0} from {2} on thread # {1}", message.Id, Thread.CurrentThread.ManagedThreadId, _channel.Name);

            await _channel.RequeueAsync(message, RequeueDelayInMilliseconds);
        }

        private TRequest TranslateMessage(Message message)
        {
            if (_messageMapper == null)
                throw new ConfigurationException($"No message mapper found for type {typeof(TRequest).FullName} for message {message.Id}.");

            _logger.Value.DebugFormat("MessagePump: Translate message {0} on thread # {1}", message.Id, Thread.CurrentThread.ManagedThreadId);

            try
            {
                return _messageMapper.MapToRequest(message);
            }
            catch (Exception exception)
            {
                throw new MessageMappingException($"Failed to map message {message.Id} using message mapper {_messageMapper.GetType().FullName} for type {typeof(TRequest).FullName}.", exception);
            }
        }

        private bool UnacceptableMessageLimitReached()
        {
            if (UnacceptableMessageLimit == 0)
                return false;

            if (_unacceptableMessageCount < UnacceptableMessageLimit)
                return false;

            _logger.Value.ErrorFormat(
                "MessagePump: Unacceptable message limit of {2} reached, stopping reading messages from {0} on thread # {1}",
                _channel.Name,
                Thread.CurrentThread.ManagedThreadId,
                UnacceptableMessageLimit);

            return true;
        }
    }
}