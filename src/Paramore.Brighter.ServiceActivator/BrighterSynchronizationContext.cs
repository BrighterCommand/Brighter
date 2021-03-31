using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.ServiceActivator
{
    internal class BrighterSynchronizationContext : SynchronizationContext
    {
        private readonly IAmAChannel _channel;

        public BrighterSynchronizationContext(IAmAChannel channel)
        {
            _channel = channel;
        }

        /// <summary>Dispatches an asynchronous message to the synchronization context.</summary>
        /// <param name="d">The System.Threading.SendOrPostCallback delegate to call.</param>
        /// <param name="state">The object passed to the delegate.</param>
        public override void Post(SendOrPostCallback callback, object state)
        {
            _channel.Enqueue(new Message(new MessageHeader(), new MessageBody(new PostBackItem(callback, state))));
        }
        
        /// <summary>Not supported.</summary>
        public override void Send(SendOrPostCallback d, object state)
        {
            throw new NotSupportedException("MessagePumpAsync does not support sending synchronously");
        }
    }
}
