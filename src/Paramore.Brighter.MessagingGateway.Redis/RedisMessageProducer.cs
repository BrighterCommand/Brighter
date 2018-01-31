using System;
using System.Collections.Generic;
using System.Linq;
using ServiceStack.Redis;
using Paramore.Brighter.MessagingGateway.Redis.LibLog;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    /*Why don't we simply use Redis Pub-Sub here?
     We don't want to use pub-sub because you can't support competing consumers and messages 'dissapper'
     if no consumer is connected. Instead, we want to implement a dynamic recipient list instead, 
     so that we can have a 'logical' queue that has multiple possible consumers.
     Each queue subscribes to a topic and has a copy of the message, but each queue might 
     have multiple consumers.
     
     See: http://blog.radiant3.ca/2013/01/03/reliable-delivery-message-queues-with-redis/
     
     We end with a 
         Recipient List: Set
         Next Topic Item No: Number
         Message: String
     And for each consumer
         Message Queue: List
         
    */
    public class RedisMessageProducer : IAmAMessageProducer
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<RedisMessageProducer>);
        private const string NEXT_ID = "nextid";
        private const string QUEUES = "queues";
        private static Lazy<RedisManagerPool> _pool;
        private readonly TimeSpan _messageTimeToLive;
        private string _topic;

        public RedisMessageProducer(RedisMessagingGatewayConfiguration redisMessagingGatewayConfiguration)
        {
            _messageTimeToLive = redisMessagingGatewayConfiguration.MessageTimeToLive ?? TimeSpan.FromMinutes(10);
            
            _pool = new Lazy<RedisManagerPool>(() => new RedisManagerPool(
                redisMessagingGatewayConfiguration.RedisConnectionString, 
                new RedisPoolConfig() {MaxPoolSize = redisMessagingGatewayConfiguration.MaxPoolSize}
            ));
            
        }

        public void Dispose()
        {
            if (_pool.IsValueCreated)
                _pool.Value.Dispose();
            
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Task.</returns>
        public void Send(Message message)
        {
            using (var client = _pool.Value.GetClient())
            {
                _topic = message.Header.Topic;

                _logger.Value.DebugFormat("RedisMessageProducer: Preparing to send message");
  
                //Convert the message into something we can put out via Redis i.e. a string
                var redisMessage = RedisMessageFactory.EMPTY_MESSAGE;
                using (var redisMessageFactory = new RedisMessageFactory())
                {
                    redisMessage = redisMessageFactory.Create(message);
                }
                
                _logger.Value.DebugFormat("RedisMessageProducer: Publishing message with topic {0} and id {1} and body: {2}", 
                    message.Header.Topic, message.Id.ToString(), message.Body.Value);
                //increment a counter to get the next message id
                var nextMsgId = IncrementMessageCounter(client);
                //store the message, against that id
                StoreMessage(client, redisMessage, nextMsgId);
                //If there are subscriber queues, push the message to the subscriber queues
                var pushedTo = PushToQueues(client, nextMsgId);
                _logger.Value.DebugFormat("RedisMessageProducer: Published message with topic {0} and id {1} and body: {2} to quues: {3}", 
                    message.Header.Topic, message.Id.ToString(), message.Body.Value, string.Join(", ", pushedTo));
             }
        }
        
        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delayMilliseconds">The sending delay</param>
        /// <returns>Task.</returns>
         public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            //No delay support implemented
            Send(message);
        }
 

        private IEnumerable<string> PushToQueues(IRedisClient client, long nextMsgId)
        {
            var key = _topic + "." + QUEUES;
            var queues = client.GetAllItemsFromSet(key).ToList();
            foreach (var queue in queues)
            {
                //First add to the queue itself
                client.AddItemToList(queue, nextMsgId.ToString());
            }
            return queues;
        }

        private void StoreMessage(IRedisClient client, string redisMessage, long nextMsgId)
        {
           //we store the message at topic + next msg id
            var key = _topic + "." + nextMsgId.ToString();
            client.SetValue(key, redisMessage, _messageTimeToLive);
        }

        private long IncrementMessageCounter(IRedisClient client)
        {
            //This holds the next id for this topic; we use that to store message contents and signal to queue
            //that there is a message to read.
            var key = _topic + "." + NEXT_ID;
            return client.IncrementValue(key);
        }

   }
}