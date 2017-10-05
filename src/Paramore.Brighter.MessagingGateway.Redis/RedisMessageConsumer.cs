using System;
using System.Collections.Concurrent;
using StackExchange.Redis;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessageConsumer : IAmAMessageConsumer
    {
        private readonly ConnectionMultiplexer _connectionMultiplexer;
        private readonly string _topic;

        private readonly BlockingCollection<Message> _messages = new BlockingCollection<Message>();
        private ISubscriber _subscriber;


        public RedisMessageConsumer(ConnectionMultiplexer connectionMultiplexer, string queueName, string topic)
        {
            _connectionMultiplexer = connectionMultiplexer;
            _topic = topic;

            _subscriber = _connectionMultiplexer.GetSubscriber();
            _subscriber.Subscribe(_topic, (channel, value) => { _messages.Add(BrighterRedisMessage.Read(value)); });
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _messages?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public Message Receive(int timeoutInMilliseconds)
        {
            return _messages.TryTake(out Message message, timeoutInMilliseconds) ? message : new Message();
        }

        public void Acknowledge(Message message)
        {
            throw new System.NotImplementedException();
        }

        public void Reject(Message message, bool requeue)
        {
            throw new System.NotImplementedException();
        }

        public void Purge()
        {
            throw new System.NotImplementedException();
        }

        public void Requeue(Message message)
        {
            throw new System.NotImplementedException();
        }
    }
}