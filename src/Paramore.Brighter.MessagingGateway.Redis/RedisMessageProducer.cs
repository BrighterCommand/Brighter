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
        private readonly ConnectionMultiplexer _connectionMultiplexer;
        private static volatile ConnectionMultiplexer _redis;

        public RedisMessageProducer(ConnectionMultiplexer connectionMultiplexer)
        {
            _connectionMultiplexer = connectionMultiplexer;
        }

        public void Dispose()
        {
        }

        public void Send(Message message)
        {
        }
    }
}