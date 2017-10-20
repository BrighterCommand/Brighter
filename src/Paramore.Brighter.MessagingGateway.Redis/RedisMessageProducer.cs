using StackExchange.Redis;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessageProducer : IAmAMessageProducer
    {
        private readonly ConnectionMultiplexer _connectionMultiplexer;
        //private static volatile ConnectionMultiplexer _redis;
        //private static readonly object SyncRoot = new object();

        public RedisMessageProducer(ConnectionMultiplexer connectionMultiplexer)
        {
            _connectionMultiplexer = connectionMultiplexer;
        }

        public void Dispose()
        {
        }

        public void Send(Message message)
        {
            var sub = _connectionMultiplexer.GetSubscriber();
            sub.Publish(message.Header.Topic, BrighterRedisMessage.Write(message));
        }
    }
}