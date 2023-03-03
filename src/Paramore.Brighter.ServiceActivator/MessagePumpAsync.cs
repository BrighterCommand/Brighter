﻿#region Licence
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
using System.Threading;
using Microsoft.Extensions.Logging;

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
        /// <summary>
        /// Constructs a message pump 
        /// </summary>
        /// <param name="commandProcessorProvider">Provides a way to grab a command processor correctly scoped</param>
        /// <param name="messageMapperRegistry">The registry of mappers</param>
        /// <param name="messageTransformerFactory">The factory that lets us create instances of transforms</param>
        public MessagePumpAsync(
            IAmACommandProcessorProvider commandProcessorProvider,
            IAmAMessageMapperRegistry messageMapperRegistry, 
            IAmAMessageTransformerFactory messageTransformerFactory = null) 
            : base(commandProcessorProvider, messageMapperRegistry, messageTransformerFactory)
        {
        }

        /// <summary>
        /// Constructs a message pump 
        /// </summary>
        /// <param name="commandProcessor">A command processor</param>
        /// <param name="messageMapperRegistry">The registry of mappers</param>
        /// <param name="messageTransformerFactory">The factory that lets us create instances of transforms</param>
        public MessagePumpAsync(
            IAmACommandProcessor commandProcessor, 
            IAmAMessageMapperRegistry messageMapperRegistry,
            IAmAMessageTransformerFactory messageTransformerFactory = null) 
            : this(new CommandProcessorProvider(commandProcessor), messageMapperRegistry, messageTransformerFactory)
        {}

        protected override void DispatchRequest(MessageHeader messageHeader, TRequest request)
        {
            s_logger.LogDebug("MessagePump: Dispatching message {Id} from {ChannelName} on thread # {ManagementThreadId}", request.Id, Thread.CurrentThread.ManagedThreadId, Channel.Name);

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
            await CommandProcessorProvider.Get().PublishAsync(request, continueOnCapturedContext: true);
        }

        private async void SendAsync(TRequest request)
        {
            await CommandProcessorProvider.Get().SendAsync(request, continueOnCapturedContext: true);
        }

    }
}
