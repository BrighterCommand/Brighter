using System;
using System.Collections.Concurrent;
using StackExchange.Redis;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessageConsumer : IAmAMessageConsumer
    {
        
        /* see RedisMessageProducer to understand how we are using a dynamic recipient list model with Redis */

        private const string QUEUES = "queues";
        
        private readonly string _queueName;
        private readonly string _topic;
        
        private static Lazy<ConnectionMultiplexer> _conn;
        private IDatabase _database;
        private ISubscriber _subscriber;

        public RedisMessageConsumer(
            RedisMessagingGatewayConfiguration redisMessagingGatewayConfiguration, 
            string queueName, 
            string topic)
        {
            _queueName = queueName;
            _topic = topic;
            _conn = new Lazy<ConnectionMultiplexer>(() =>
            {
                var options = ConfigurationOptions.Parse(redisMessagingGatewayConfiguration.ServerList);
                options.AllowAdmin = redisMessagingGatewayConfiguration.AllowAdmin;
                options.ConnectRetry = redisMessagingGatewayConfiguration.ConnectRetry;
                options.ConnectTimeout = redisMessagingGatewayConfiguration.ConnectTimeout;
                options.SyncTimeout = redisMessagingGatewayConfiguration.SyncTimeout;
                options.Proxy = redisMessagingGatewayConfiguration.Proxy;
                     
                return ConnectionMultiplexer.Connect(options);
            });
        }

        private void Dispose(bool disposing)
        {
            if (_conn.IsValueCreated)
                _conn.Value.Dispose();
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
            //This kills the queue, not the messages, which we assume expire
            _database.ListTrim(_queueName, 0, _database.ListLength(_queueName) - 1);
        }

        public Message Receive(int timeoutInMilliseconds)
        {
            _database = _conn.Value.GetDatabase();
            _subscriber = _conn.Value.GetSubscriber();
  
            
            EnsureConnection();
            var redisMessage = ReadMessage();
            return new BrighterMessageFactory().Create(redisMessage);
        }

       public void Reject(Message message, bool requeue)
        {
            /* This a 'do nothing operation' until we have an invalid message queue */
        }

        public void Requeue(Message message)
        {
            //Push the Id onto our own queue
        }

        private void EnsureConnection()
        {
            //what is the queue list key
            var key = _topic + "." + QUEUES;
            //subscribe us 
            _database.SetAdd(key, _queueName);

        }

        private string ReadMessage()
        {
            var msg = string.Empty;
            //TODO: This is a callback which means that we always return an empty message, as the delegate is called
            //and does not return from this function.
            _subscriber.Subscribe(_queueName, (channel, message) =>
            {
                var expectedId = Convert.ToInt64(message);
                var latestId = Convert.ToInt64(_database.ListRightPop(_queueName).ToString());
                if (latestId <= expectedId)
                {
                   //TODO: Log this, we want you to know that we didn't get the expected item, probably
                   //TODO: a benign competing consumer issue 
                }
                var key = _topic + "." + latestId.ToString();
                msg = _database.StringGet(key);
            });
            return msg;
        }
    }
}