using System.Threading;

namespace Paramore.Brighter.ServiceActivator
{
    internal class MessagePumpSynchronizationContext : SynchronizationContext
    {
        private readonly IAmAChannel _channel;

        public MessagePumpSynchronizationContext(IAmAChannel channel)
        {
            _channel = channel;
        }

        public override void Post(SendOrPostCallback callback, object state)
        {
            _channel.Enqueue(new Message(new MessageHeader(), new MessageBody(new PostBackItem(callback, state))));
        }
    }
}
