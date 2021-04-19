using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Used when we don't want to block for I/O, but queue on a completion port and be notified when done
    /// Adopts a single-threaded apartment model. We have one thread, all work - messages and calbacks is queued to that a single work queue
    /// When a callback is signalled it is queued next, and will be picked up when the current message completes or waits itself
    /// Strict ordering of messages will be lost as no guarantee what order I/O operations will complete - do not use if strict ordering required
    /// Only used one thread, so predictable performance, but may have many messages queued. Once queue length exceeds buffer size, we will stop reading new work
    /// Based on https://devblogs.microsoft.com/pfxteam/await-synchronizationcontext-and-console-apps/
    /// </summary>
    /// <typeparam name="TRequest">The Request on the Data Type Channel</typeparam>
    public class MessagePumpAsync<TRequest> : MessagePump<TRequest> where TRequest : class, IRequest
    {
        public MessagePumpAsync(
            IAmACommandProcessor commandProcessor, 
            IAmAMessageMapper<TRequest> messageMapper) 
            : base(commandProcessor, messageMapper)
        {
        }

        protected override void DispatchRequest(MessageHeader messageHeader, TRequest request)
        {
            _logger.Value.DebugFormat("MessagePump: Dispatching message {0} from {2} on thread # {1}", request.Id, Thread.CurrentThread.ManagedThreadId, Channel.Name);

            var messageType = messageHeader.MessageType;
            
            ValidateMessageType(messageType, request);

            switch (messageType)
            {
                case MessageType.MT_COMMAND:
                {
                    Run(SendAsync, request);
                    break;
                }
                case MessageType.MT_DOCUMENT:
                case MessageType.MT_EVENT:
                {
                    Run(PublishAsync, request);
                    break;
                }
            }
        }
        
        private static void Run(Action<TRequest> act, TRequest request)
        {
            if (act == null) throw new ArgumentNullException("act");

            var prevCtx = SynchronizationContext.Current;
            try
            {
                // Establish the new context
                var context = new BrighterSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(context);

                context.OperationStarted();

                act(request);

                context.OperationCompleted();

                // Pump continuations and propagate any exceptions
                context.RunOnCurrentThread();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevCtx);
            }
        }
        
        private async void PublishAsync(TRequest request)
        {
            await _commandProcessor.PublishAsync(request, continueOnCapturedContext: true);
        }

        private async void SendAsync(TRequest request)
        {
            await _commandProcessor.SendAsync(request, continueOnCapturedContext: true);
        }

    }
}
