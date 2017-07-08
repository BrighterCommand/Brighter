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

using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.ServiceActivator.Logging;

namespace Paramore.Brighter.ServiceActivator
{
    public class MessagePump<TRequest> : MessagePumpBase<TRequest>, IAmAMessagePump where TRequest : class, IRequest
    {
        public MessagePump(IAmAChannel channel, IAmACommandProcessor commandProcessor, IAmAMessageMapper<TRequest> messageMapper)
            : base(channel, commandProcessor, messageMapper)
        {}

        protected override Task DispatchRequest(MessageHeader messageHeader, TRequest request, CancellationToken cancellationToken)
        {
            _logger.Value.DebugFormat("MessagePump: Dispatching message {0} from {2} on thread # {1}", request.Id, Thread.CurrentThread.ManagedThreadId, _channel.Name);

            if (messageHeader.MessageType == MessageType.MT_COMMAND && request is IEvent)
                throw new ConfigurationException($"Message {request.Id} mismatch. Message type is '{MessageType.MT_COMMAND}' yet mapper produced message of type {nameof(IEvent)}");

            if (messageHeader.MessageType == MessageType.MT_EVENT && request is ICommand)
                throw new ConfigurationException($"Message {request.Id} mismatch. Message type is '{MessageType.MT_EVENT}' yet mapper produced message of type {nameof(ICommand)}");

            switch (messageHeader.MessageType)
            {
                case MessageType.MT_COMMAND:
                    _commandProcessor.Send(request);
                    break;
                case MessageType.MT_DOCUMENT:
                case MessageType.MT_EVENT:
                    _commandProcessor.Publish(request);
                    break;
            }

            return Task.CompletedTask;
        }
    }
}