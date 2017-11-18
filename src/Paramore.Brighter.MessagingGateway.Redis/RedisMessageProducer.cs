using System;
using StackExchange.Redis;

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
         
     But, we don't want to use BRPOP or BLPOP because StackExchange Redis, which is a multiplexed library, and so can't 
     use these blocking operations. The recommended workaround is using PubSub to signal to workers to read from their 
     queue. 
     
     See: https://stackexchange.github.io/StackExchange.Redis/PipelinesMultiplexers
     
     sub.Subscribe(channel, delegate {
        string work = db.ListRightPop(key);
        if (work != null) Process(work);
     });
        
     //...
     db.ListLeftPush(key, newWork, flags: CommandFlags.FireAndForget);
     sub.Publish(channel, "");
     
     So. although we use the pub-sub API, but not to do pub-sub. Make sense?
    */
    public class RedisMessageProducer : IAmAMessageProducer
    {
        private const string NEXT_ID = "";
        private const string QUEUES = "queues";
        private static Lazy<ConnectionMultiplexer> _conn;
        private readonly TimeSpan _messageTimeToLive;
        private IDatabase _database;
        private ISubscriber _subscriber;
        private string _topic;

        public RedisMessageProducer(RedisMessagingGatewayConfiguration redisMessagingGatewayConfiguration)
        {
            _messageTimeToLive = redisMessagingGatewayConfiguration.MessageTimeToLive ?? TimeSpan.FromMinutes(10);
            
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

        public void Dispose()
        {
            if (_conn.IsValueCreated)
                _conn.Value.Dispose();
        }

        public void Send(Message message)
        {
            _database = _conn.Value.GetDatabase();
            _subscriber = _conn.Value.GetSubscriber();
            _topic = message.Header.Topic;
            
            //Convert the message into something we can put out via Redis i.e. a string
            var redisMessage = RedisMessageFactory.EMPTY_MESSAGE;
            using (var redisMessageFactory = new RedisMessageFactory())
            {
                redisMessage = redisMessageFactory.Create(message);
            }
            //increment a counter to get the next message id
            var nextMsgId = IncrementMessageCounter();
            //store the message, against that id
            StoreMessage(redisMessage, nextMsgId);
            //If there are subscriber queues, push the message to the subscriber queues
            PushToQueues(nextMsgId);
        }

        private void PushToQueues(long nextMsgId)
        {
            var key = _topic + "." + QUEUES;
            var queues = _database.SetMembers(key);
            foreach (var queue in queues)
            {
                //First add to the queue itself
                _database.ListLeftPush(queue.ToString(), nextMsgId, flags: CommandFlags.FireAndForget);
                //Now signal the consumers that there is work on the queue to read
                _subscriber.Publish(_topic, nextMsgId, flags:CommandFlags.FireAndForget);
            }
        }

        private void StoreMessage(string redisMessage, long nextMsgId)
        {
           //we store the message at topic + next msg id
            var key = _topic + "." + nextMsgId.ToString();
            _database.StringSet(key, redisMessage, _messageTimeToLive, flags: CommandFlags.FireAndForget);
        }

        private long IncrementMessageCounter()
        {
            //This holds the next id for this topic; we use that to store message contents and signal to queue
            //that there is a message to read.
            var key = _topic + "." + NEXT_ID;
            return _database.StringIncrement(key);
        }

   }
}