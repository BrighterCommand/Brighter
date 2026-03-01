#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Reflection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Used to determine the status of the message pump
    /// </summary>
    /// <remarks>
    /// This is intended to make testing easier - how do we figure out how the message pump status, particularly if
    /// we want to trace an error path
    /// </remarks>
    public enum MessagePumpStatus
    {
        /// <summary> The message pump has been started</summary>
        MP_STARTED,
        /// <summary> The message pump terminated because we sent it a quit message; normal shutdown </summary>
        MP_STOPPED,
        /// <summary> The message pump terminated via an unhandled exception </summary>
        MP_ERROR,
        /// <summary> The message pump terminated because the unacceptable message was breached within its window</summary>
        MP_LIMIT_EXCEEDED,
    }
    
    
    /// <summary>
    /// The message pump is the heart of a consumer. It runs a loop that performs the following:
    ///  - Gets a message from a queue/stream
    ///  - Translates the message to the local type system
    ///  - Dispatches the message to waiting handlers
    /// The message pump is a classic event loop and is intended to be run on a single-thread
    /// The event loop is terminated when reading a MT_QUIT message on the channel
    /// The event loop blocks on the Channel Listen call, though it will timeout
    /// The event loop calls user code synchronously. You can post again for further decoupled invocation, but of course the likelihood is we are supporting decoupled invocation elsewhere
    /// This is why you should spin up a thread for your message pump: to avoid blocking your main control path while you listen for a message and process it
    /// It is also why throughput on a queue needs multiple performers, each with their own message pump
    /// Retry and circuit breaker should be provided by exception policy using an attribute on the handler
    /// Timeout on the handler should be provided by timeout policy using an attribute on the handler 
    /// </summary>
    public abstract partial class MessagePump
    {
        internal static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MessagePump>();

        protected readonly IAmACommandProcessor CommandProcessor;
        protected readonly IAmARequestContextFactory RequestContextFactory;
        protected readonly IAmABrighterTracer? Tracer;
        protected readonly InstrumentationOptions InstrumentationOptions;
        protected int UnacceptableMessageCount;
        protected readonly Dictionary<Type, MethodInfo> UnWrapPipelineFactoryCache = new();
        protected readonly Dictionary<Type, MethodInfo> DispatchMethodCache = new();
        protected DateTimeOffset? UnacceptableMessageWindowOpenedA = null;
        
        /// <summary>
        /// The delay to wait when the channel has failed
        /// </summary>
        public TimeSpan ChannelFailureDelay { get; set; }

        /// <summary>
        /// The delay to wait before the next pump iteration after a <see cref="Actions.DontAckAction"/>.
        /// Prevents tight-loop CPU burn when a message is repeatedly not acknowledged.
        /// </summary>
        public TimeSpan DontAckDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The delay to wait when the channel is empty
        /// </summary>
        public TimeSpan EmptyChannelDelay { get; set; }
        
        /// <summary>
        /// The <see cref="MessagePumpType"/> of this message pump; indicates Reactor or Proactor
        /// </summary>
        public abstract MessagePumpType MessagePumpType { get; }
        
        /// <summary>
        /// Sets the <see cref="TimeProvider"/> used by the pump. Defaults to TimeProviderSystem
        /// </summary>
        /// <remarks>
        /// Allows you to override the time provider, intended for testing purposes. 
        /// </remarks>
        public TimeProvider PumpTimeProvider { get; set; }
  
        /// <summary>
        /// How many times to requeue a message before discarding it
        /// </summary>
        public int RequeueCount { get; set; }

        /// <summary>
        /// How long to wait before requeuing a message
        /// </summary>
        public TimeSpan RequeueDelay { get; set; }
        
        /// <summary>
        /// The <see cref="MessagePumpStatus"/> of the pump
        /// </summary>
        public MessagePumpStatus Status { get; set; }
        
        /// <summary>
        /// How long to wait for a message before timing out
        /// </summary>
        public TimeSpan TimeOut { get; set; }

        /// <summary>
        /// The number of unacceptable messages to receive before stopping the message pump
        /// </summary>
        public int UnacceptableMessageLimit { get; set; }
        
        /// <summary>
        /// Gets the window in which we monitor the unacceptable message count. The count resets at the end of the window.
        /// If null, the count never resets.
        /// </summary>
        public TimeSpan? UnacceptableMessageLimitWindow  { get; set; }

        /// <summary>
        /// Constructs a message pump. The message pump is the heart of a consumer. It runs a loop that performs the following:
        ///  - Gets a message from a queue/stream
        ///  - Translates the message to the local type system
        ///  - Dispatches the message to waiting handlers
        ///  The message pump is a classic event loop and is intended to be run on a single-thread 
        /// </summary>
        /// <param name="commandProcessor">Provides a correctly scoped command processor </param>
        /// <param name="requestContextFactory">Provides a request synchronizationHelper</param>
        /// <param name="tracer">What is the <see cref="BrighterTracer"/> we will use for telemetry</param>
        /// <param name="channel"></param>
        /// <param name="instrumentationOptions">When creating a span for <see cref="Brighter.CommandProcessor"/> operations how noisy should the attributes be</param>
        /// <param name="timeProvider">Allows you to override the time provider, for testing purposes</param>
        protected MessagePump(
            IAmACommandProcessor commandProcessor, 
            IAmARequestContextFactory requestContextFactory,
            IAmABrighterTracer? tracer,
            InstrumentationOptions instrumentationOptions = InstrumentationOptions.All,
            TimeProvider? timeProvider = null)
        {
            CommandProcessor = commandProcessor;
            RequestContextFactory = requestContextFactory;
            Tracer = tracer;
            InstrumentationOptions = instrumentationOptions;
            PumpTimeProvider = timeProvider ?? TimeProvider.System;
        }


        protected bool DiscardRequeuedMessagesEnabled()
        {
            return RequeueCount != -1;
        }

        protected void IncrementUnacceptableMessageCount()
        {
            if (UnacceptableMessageWindowOpenedA is null)
                UnacceptableMessageWindowOpenedA = PumpTimeProvider.GetUtcNow();
            
            var timeSinceWindowOpened = PumpTimeProvider.GetUtcNow() - UnacceptableMessageWindowOpenedA.Value;
            if (UnacceptableMessageLimitWindow.HasValue && timeSinceWindowOpened > UnacceptableMessageLimitWindow)
            {
                UnacceptableMessageCount = 0;
                UnacceptableMessageWindowOpenedA = PumpTimeProvider.GetUtcNow();
            }

            UnacceptableMessageCount++;
        }

        protected void ValidateMessageType(MessageType messageType, IRequest request)
        {
            if (messageType == MessageType.MT_COMMAND && request is IEvent)
            {
                Log.MessageMismatchCommand(s_logger, request.Id, MessageType.MT_COMMAND);
            }

            if (messageType == MessageType.MT_EVENT && request is ICommand)
            {
                Log.MessageMismatchEvent(s_logger, request.Id, MessageType.MT_EVENT);
            }
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Error, "Message {MessageId} mismatch. Message type is '{MessageType}' yet mapper produced message of type IEvent")]
            public static partial void MessageMismatchCommand(ILogger logger, string messageId, MessageType messageType);

            [LoggerMessage(LogLevel.Error, "Message {MessageId} mismatch. Message type is '{MessageType}' yet mapper produced message of type ICommand")]
            public static partial void MessageMismatchEvent(ILogger logger, string messageId, MessageType messageType);
        }
   }
}

