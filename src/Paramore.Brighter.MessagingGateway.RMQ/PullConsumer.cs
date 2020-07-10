using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Paramore.Brighter.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Paramore.Brighter.MessagingGateway.RMQ
{
    public class PullConsumer : DefaultBasicConsumer
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<RmqMessageConsumer>);
        
        //we do end up creating a second buffer to the Brighter Channel, but controlling the flow from RMQ depends
        //on us being able to buffer up to the set QoS and then pull. This matches other implementations.
        private readonly ConcurrentQueue<BasicDeliverEventArgs> _messages = new ConcurrentQueue<BasicDeliverEventArgs>();

        public PullConsumer(IModel channel)
            :base(channel)
        {}

        /// <summary>
        /// Used to pull from the buffer of messages delivered to us via BasicConsumer
        /// </summary>
        /// <param name="timeoutInMilliseconds">The total time to spend waiting for the buffer to fill up to bufferSize</param>
        /// <param name="bufferSize">The size of the buffer we want to fill wit messages</param>
        /// <returns>A tuple containing: the number of messages in the buffer, and the buffer itself</returns>
        public (int, BasicDeliverEventArgs[]) DeQueue(int timeoutInMilliseconds, int bufferSize)
        {
            var now = DateTime.UtcNow;
            var end = now.AddMilliseconds(timeoutInMilliseconds);
            var pause = ((timeoutInMilliseconds > 25) && (timeoutInMilliseconds / 5 > 5)) ? timeoutInMilliseconds / 5 : 5;
 
            
            var buffer = new BasicDeliverEventArgs[bufferSize];
            var bufferIndex = 0;
            
            
            while (now < end && bufferIndex < bufferSize)
            {
                if (_messages.TryDequeue(out BasicDeliverEventArgs result))
                {
                    buffer[bufferIndex] = result;
                    ++bufferIndex;
                }
                else
                {
                    Task.Delay(pause).Wait();
                }
                now = DateTime.UtcNow;
            }

            return bufferIndex == 0 ? (0, null) : (bufferIndex, buffer);
        }
        
         public override void HandleBasicDeliver(
            string consumerTag, 
            ulong deliveryTag, 
            bool redelivered, 
            string exchange, 
            string routingKey,
            IBasicProperties properties,
            ReadOnlyMemory<byte> body)
        {
            //We have to copy the body, before returning, as the memory in body is pooled and may be re-used after (see base class documentation)
            //See also https://docs.microsoft.com/en-us/dotnet/standard/memory-and-spans/memory-t-usage-guidelines
            var payload = new byte[body.Length];
            body.CopyTo(payload);

            _messages.Enqueue(new BasicDeliverEventArgs
            {
                BasicProperties = properties,
                Body = payload,
                ConsumerTag = consumerTag,
                DeliveryTag = deliveryTag,
                Exchange = exchange,
                Redelivered = redelivered,
                RoutingKey = routingKey
            });
        }

        public override void OnCancel(params string[] consumerTags)
        {
            //try  to nack anything in the buffer.
            try
            {
                var messages = _messages.ToArray();
                foreach (var message in _messages)
                {
                    Model.BasicNack(message.DeliveryTag, false, true);
                }
            }
            catch (Exception e)
            {
                //don't impede shutdown, just log
                _logger.Value.WarnFormat("Tried to nack unhandled messages on shutdown but failed for {0}",
                    e.Message);
            }
           
            base.OnCancel();
        }

   }
}
