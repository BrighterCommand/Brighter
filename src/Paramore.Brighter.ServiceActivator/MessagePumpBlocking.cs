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

using System.Threading;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Used when the message pump should block for I/O
    /// Will guarantee strict ordering of the messages on the queue
    /// Predictable performance as only one thread, allows you to configure number of performers for number of threads to use
    /// Lower throughput than async
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    public class MessagePumpBlocking<TRequest> : MessagePump<TRequest> where TRequest : class, IRequest
    {
        /// <summary>
        /// Constructs a message pump 
        /// </summary>
        /// <param name="commandProcessorProvider">Provides a way to grab a command processor correctly scoped</param>
        /// <param name="messageMapperRegistry">The registry of mappers</param>
        /// <param name="messageTransformerFactory">The factory that lets us create instances of transforms</param>
        public MessagePumpBlocking(
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
        public MessagePumpBlocking(
            IAmACommandProcessor commandProcessor, 
            IAmAMessageMapperRegistry messageMapperRegistry,
            IAmAMessageTransformerFactory messageTransformerFactory = null) :
            this(new CommandProcessorProvider(commandProcessor), messageMapperRegistry, messageTransformerFactory)
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
                    CommandProcessorProvider.Get().Send(request);
                    break;
                }
                case MessageType.MT_DOCUMENT:
                case MessageType.MT_EVENT:
                {
                    CommandProcessorProvider.Get().Publish(request);
                    break;
                }
            }
        }
    }
}
