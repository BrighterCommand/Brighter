using System;
using System.Net.Http;
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
 
        /// <summary>
        /// Creates a consumer that reads from a List in Redis via a BLPOP (so will block).
        /// </summary>
        /// <param name="redisMessagingGatewayConfiguration">Configuration for our Redis cient etc.</param>
        /// <param name="queueName">Key of the list in Redis we want to read from</param>
        /// <param name="topic">The topic that the list subscribes to</param>
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

        /// <summary>
        /// This a 'do nothing operation' as with Redis we pop the message from the queue to read;
        /// this allows us to have competing consumers, and thus a message is always 'consumed' even
        /// if we fail to process it.
        /// The risk with Redis is that we lose any in-flight message if we kill the service, without allowing
        /// the job to run to completion. Brighter uses run to completion if shut down properly, but not if you
        /// just kill the process.
        /// If you need the level of reliability that unprocessed messages that return to the queue don't use Redis.
        /// </summary>
        /// <param name="message"></param>
        public void Acknowledge(Message message)
        {
            _logger.Value.InfoFormat("RmqMessageConsumer: Acknowledging message {0}", message.Id.ToString());
        }

        /// <summary>
        /// Free up our RedisMangerPool, connections not held open between invocations of Receive, so you can create
        /// a consumer and keep it for program lifetime, disposing at the end only, without fear of a leak
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Clear the queue
        /// </summary>
        public void Purge()
        {
            using (var client = _pool.Value.GetClient())
            {
                _logger.Value.DebugFormat("RmqMessageConsumer: Purging channel {0}", _queueName);
                //This kills the queue, not the messages, which we assume expire
                client.RemoveAllFromList(_queueName);
            }
        }

        /// <summary>
        /// Get the next message off the Redis list, within a timeout
        /// </summary>
        /// <param name="timeoutInMilliseconds">The period to await a message</param>
        /// <returns>The message read from the list</returns>
        public Message Receive(int timeoutInMilliseconds)
        {
            _logger.Value.DebugFormat("RedisMessageConsumer: Preparing to retrieve next message from queue {0} with routing key {1} via exchange {2} on connection {3}", _queueName, _topic);
            var message = new Message();
            IRedisClient client = null;
            try
            {
                client = GetClient();
                EnsureConnection(client);
                var redisMessage = ReadMessage(client, timeoutInMilliseconds);
                message = new BrighterMessageFactory().Create(redisMessage);
            }
            catch (TimeoutException te)
            {
                _logger.Value.ErrorFormat("Could not connect to Redis client within {0} milliseconds", timeoutInMilliseconds.ToString());
                throw new ChannelFailureException(
                    string.Format("Could not connect to Redis client within {0} milliseconds", timeoutInMilliseconds.ToString()),
                    te
                );
            }
            catch (RedisException re)
            {
                _logger.Value.ErrorFormat($"Could not connect to Redis: {re.Message}");
                throw new ChannelFailureException(string.Format("Could not connect to Redis client - see inner exception for details" ), re);
                 
            }
            finally
            {
                client?.Dispose();
            }
            return message;
        }


        /// <summary>
        /// This a 'do nothing operation' as we have already popped
        /// </summary>
        /// <param name="message"></param>
        /// <param name="requeue"></param>
        public void Reject(Message message, bool requeue)
        {
        }

        /// <summary>
        /// Push the Id back onto the queue, to re-order
        /// </summary>
        /// <param name="message"></param>
        public void Requeue(Message message)
        {
        }

        /*Virtual to allow testing to simulate client failure*/
        protected virtual IRedisClient GetClient()
        {
            return _pool.Value.GetClient();
        }
        
        private void Dispose(bool disposing)
        {
            if (_pool.IsValueCreated)
                _pool.Value.Dispose();
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