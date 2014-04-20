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

using paramore.brighter.commandprocessor;

namespace paramore.brighter.messagedispatcher
{
    public class MessagePump<TRequest> : IAmAMessagePump<TRequest> where TRequest : class, IRequest
    {
        private readonly IMessageChannel channel;
        private readonly IAmACommandProcessor commandProcessor;
        private readonly IAmAMessageMapper<TRequest> messageMapper;
        private readonly int timeoutInMilliseconds;

        public MessagePump(IMessageChannel channel, IAmACommandProcessor commandProcessor, IAmAMessageMapper<TRequest> messageMapper, int timeoutInMilliseconds)
        {
            this.channel = channel;
            this.commandProcessor = commandProcessor;
            this.messageMapper = messageMapper;
            this.timeoutInMilliseconds = timeoutInMilliseconds;
        }

        public void Run()
        {
            do
            {
                var message = channel.Listen(timeoutInMilliseconds);
                
                if (message == null)
                {
                    //TODO: delay
                    continue;
                }

                if (message.Header.MessageType == MessageType.MT_QUIT)
                    break;

                var request = TranslateMessage(message);
                DispatchRequest(message.Header.MessageType, request);

            } while (true);
        }

        private void DispatchRequest(MessageType messageType, TRequest request)
        {
            switch (messageType)
            {
                case MessageType.MT_NONE:
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