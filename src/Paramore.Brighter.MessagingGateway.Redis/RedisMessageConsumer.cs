using System;
using Newtonsoft.Json;
using ServiceStack.Redis;
using Paramore.Brighter.MessagingGateway.Redis.LibLog;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessageConsumer : IAmAMessageConsumer
    {
        
        /* see RedisMessageProducer to understand how we are using a dynamic recipient list model with Redis */

        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<RedisMessageConsumer>);
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
             _logger.Value.InfoFormat("RmqMessageConsumer: Acknowledging message {0}", message.Id.ToString());
        }

        public void Purge()
        {
            using (var client = _pool.Value.GetClient())
            {
                _logger.Value.DebugFormat("RmqMessageConsumer: Purging channel {0}", _queueName);
                //This kills the queue, not the messages, which we assume expire
                client.RemoveAllFromList(_queueName);
            }
        }

        public Message Receive(int timeoutInMilliseconds)
        {
            _logger.Value.DebugFormat("RedisMessageConsumer: Preparing to retrieve next message from queue {0} with routing key {1} via exchange {2} on connection {3}", _queueName, _topic);
            var message = new Message();
            using (var client = _pool.Value.GetClient())
            {
                EnsureConnection(client);
                var redisMessage = ReadMessage(client, timeoutInMilliseconds);
                message = new BrighterMessageFactory().Create(redisMessage);
            }
            return message;
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
            _logger.Value.DebugFormat("RedisMessagingGateway: Creating queue {0}", _queueName);
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
                _logger.Value.InfoFormat(
                    "Redis: Received message from queue {0} with routing key {0}, message: {1}",
                    _queueName, _topic, JsonConvert.SerializeObject(msg), Environment.NewLine);
         }
            else
            {
               _logger.Value.DebugFormat(
                   "RmqMessageConsumer: Time out without receiving message from queue {0} with routing key {1}",
                    _queueName, _topic);
  
            }
            return msg;
        }
    }
}