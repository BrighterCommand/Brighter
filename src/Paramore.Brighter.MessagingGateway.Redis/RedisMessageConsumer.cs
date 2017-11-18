using System;
using ServiceStack.Redis;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessageConsumer : IAmAMessageConsumer
    {
        
        /* see RedisMessageProducer to understand how we are using a dynamic recipient list model with Redis */

        private const string QUEUES = "queues";
        
        private readonly string _queueName;
        private readonly string _topic;
        
        private static Lazy<RedisManagerPool> _pool;
 
        public RedisMessageConsumer(
            RedisMessagingGatewayConfiguration redisMessagingGatewayConfiguration, 
            string queueName, 
            string topic)
        {
            _queueName = queueName;
            _topic = topic;
            _pool = new Lazy<RedisManagerPool>(() => new RedisManagerPool(
                redisMessagingGatewayConfiguration.RedisConnectionString, 
                new RedisPoolConfig() {MaxPoolSize = redisMessagingGatewayConfiguration.MaxPoolSize}
            ));
        }

        private void Dispose(bool disposing)
        {
            if (_pool.IsValueCreated)
                _pool.Value.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        public void Acknowledge(Message message)
        {
            /* This a 'do nothing operation' as with Rediws we pop the message from the queue to read;
             * this allows us to have competing consumers, and thus a message is always 'consumed' even
             * if we fail to process it.
             * The risk with Redis is that we lose any in-flight message if we kill the service, without allowing
             * the job to run out.
             * If you need that level of reliability, don't use Redis.
             */
        }

        public void Purge()
        {
            using (var client = _pool.Value.GetClient())
            {
                //This kills the queue, not the messages, which we assume expire
                client.RemoveAllFromList(_queueName);
            }
        }

        public Message Receive(int timeoutInMilliseconds)
        {
            using (var client = _pool.Value.GetClient())
            {
                EnsureConnection(client);
                var redisMessage = ReadMessage(client, timeoutInMilliseconds);
                return new BrighterMessageFactory().Create(redisMessage);
            }
        }

       public void Reject(Message message, bool requeue)
        {
            /* This a 'do nothing operation' until we have an invalid message queue */
        }

        public void Requeue(Message message)
        {
            //Push the Id onto our own queue
        }

        private void EnsureConnection(IRedisClient client)
        {
            //what is the queue list key
            var key = _topic + "." + QUEUES;
            //subscribe us 
            client.AddItemToSet(key, _queueName);

        }

        private string ReadMessage(IRedisClient client, int timeoutInMilliseconds)
        {
            var msg = string.Empty;
            var latestId = client.BlockingDequeueItemFromList(_queueName, TimeSpan.FromMilliseconds(timeoutInMilliseconds));
            if (latestId != null)
            {
                var key = _topic + "." + latestId;
                msg = client.GetValue(key);
            }
            return msg;
        }
    }
}