using System;
using System.Collections.Concurrent;
using StackExchange.Redis;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessageConsumer : IAmAMessageConsumer
    {
        private readonly ConnectionMultiplexer _connectionMultiplexer;

        public RedisMessageConsumer(ConnectionMultiplexer connectionMultiplexer, string queueName, string topic)
        {
            _connectionMultiplexer = connectionMultiplexer;
        }

        private void Dispose(bool disposing)
        {
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        public void Acknowledge(Message message)
        {
            throw new System.NotImplementedException();
        }

        public void Purge()
        {
            throw new System.NotImplementedException();
        }

        public Message Receive(int timeoutInMilliseconds)
        {
            EnsureConnection();
            //return ReadMessage()
            return null;
        }

        /*This a 'do nothing operation' untile we have an invalid message queue
         * as we pop the message from the queue before reading, to ensure
         * we can have competing consumers, and thus a message is always 'consumed' even
         * if we fail to process it
         * The risk with Redis is that we lose any in-flight message if we kill the
         * service.
         * If you need that level of reliability, don't use Redis.
         */
        public void Reject(Message message, bool requeue)
        {
            
        }

        public void Requeue(Message message)
        {
            //Push the Id onto our own queue
        }

        private void EnsureConnection()
        {
            //if there is no subscription for this topic
            //create the subscription
            //subscribe us using ZSET - overwrites an existing entry

        }

        private void ReadMessage()
        {
            //pop the next id of the queue (BLPOP with timeout)
            //if we have a message id
                //read the message
                 
            
        }
    }
}