using System;
using System.Threading.Tasks;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.serviceactivator
{
    public class Performer<TRequest> : IAmAPerformer where TRequest : class, IRequest
    {
        private readonly IAmAMessageChannel channel;
        private readonly IAmAMessagePump<TRequest> messagePump;

        public Performer(IAmAMessageChannel channel, IAmAMessagePump<TRequest> messagePump)
        {
            this.channel = channel;
            this.messagePump = messagePump;
        }
        public void Stop()
        {
            channel.Enqueue(CreateQuitMessage());
        }

        public Task Run()
        {
            return Task.Factory.StartNew(() => messagePump.Run());
        }

        private Message CreateQuitMessage()
        {
            return new Message(new MessageHeader(Guid.Empty, string.Empty, MessageType.MT_QUIT), new MessageBody(string.Empty));
        }
    }
}