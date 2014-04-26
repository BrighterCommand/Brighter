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

using System.Threading.Tasks;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.serviceactivator
{
    /*
     * The message pump is a classic event loop and is intended to be run on a single-thread
     * The event loop is terminated when reading a MT_QUIT message on the channel
     * The event loop blocks on the Channel Listen call, though it will timeout
     * The event loop calls user code synchronously. You can post again for further decoupled invocation, but of course the likelihood is we are supporting decoupled invocation elsewhere
     * This is why you should spin up a thread for your message pump: to avoid blocking your main control path while you listen for a message and process it
     * It is also why throughput on a queue needs multiple performers, each with their own message pump
     * Retry and circuit breaker should be provided by exception policy using an attribute on the handler
     * Timeout on the handler should be provided by timeout policy using an attribute on the handler
     */
    public class MessagePump<TRequest> : IAmAMessagePump where TRequest : class, IRequest
    {
        private readonly IAmACommandProcessor commandProcessor;
        private readonly IAmAMessageMapper<TRequest> messageMapper;
        public int TimeoutInMilliseconds { get; set; }
        public IAmAnInputChannel Channel { get; set; }

        public MessagePump(IAmACommandProcessor commandProcessor, IAmAMessageMapper<TRequest> messageMapper)
        {
            this.Channel = Channel;
            this.commandProcessor = commandProcessor;
            this.messageMapper = messageMapper;
        }

        public void Run()
        {
            do
            {
                var message = Channel.Receive(TimeoutInMilliseconds);
                
                if (message.Header.MessageType == MessageType.MT_NONE)
                {
                    Task.Delay(500).Wait();
                    continue;
                }

                if (message.Header.MessageType == MessageType.MT_QUIT)
                    break;

                DispatchRequest(message.Header.MessageType, TranslateMessage(message));
                AcknowledgeMessage(message);

            } while (true);
        }

        private void AcknowledgeMessage(Message message)
        {
            Channel.Acknowledge(message);
        }

        private void DispatchRequest(MessageType messageType, TRequest request)
        {
            switch (messageType)
            {
                case MessageType.MT_COMMAND:
                    {
                        commandProcessor.Send(request);
                        break;
                    }
                case MessageType.MT_DOCUMENT:
                case MessageType.MT_EVENT:
                    {
                        commandProcessor.Publish(request);
                        break;
                    }
            }
        }

        private TRequest TranslateMessage(Message message)
        {
            return messageMapper.MapToRequest(message);
        }
    }
}