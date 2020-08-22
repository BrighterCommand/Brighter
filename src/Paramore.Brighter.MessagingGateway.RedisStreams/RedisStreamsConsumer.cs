#region Licence
/* The MIT License (MIT)
Copyright © 2017 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Paramore.Brighter.Logging;
using StackExchange.Redis;

namespace Paramore.Brighter.MessagingGateway.RedisStreams
{
    public class RedisStreamsConsumer : RedisStreamsGateway, IAmAMessageConsumer
    {
        
        /* see RedisMessageProducer to understand how we are using a dynamic recipient list model with Redis */

        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<RedisStreamsConsumer>);
        private readonly string _queueName;
        private readonly string _consumerGroup;
        private readonly int _batchSize;
        private readonly string _consumerId;
        private readonly IDatabase _db;

        /// <summary>
        /// Creates a consumer that reads from a List in Redis via a BLPOP (so will block).
        /// </summary>
        /// <param name="redisStreamsConfiguration">Configuration for our Redis cient etc.</param>
        /// <param name="queueName">Key of the list in Redis we want to read from</param>
        /// <param name="topic">The topic that the list subscribes to</param>
        public RedisStreamsConsumer(
            RedisStreamsConfiguration redisStreamsConfiguration,
            string queueName)
            : base(redisStreamsConfiguration)
        {
            _batchSize = redisStreamsConfiguration.BatchSize;
            _consumerGroup = redisStreamsConfiguration.ConsumerGroup;
            _consumerId = redisStreamsConfiguration.ConsumerId;
            _queueName = queueName;
            
            _db = Redis.GetDatabase();

            //if we can't create the group, fail fast (RAII)
            if (!_db.StreamCreateConsumerGroup(queueName, redisStreamsConfiguration.ConsumerGroup, StreamPosition.NewMessages))
            {
                throw new InvalidOperationException($"Not able to create the consumer group for consumer: {_consumerId}");
            }

        }

        /// <summary>
        /// Free up our Redis connection. Connections are relatively expensive, so the Conumer can be kept on open for the lifetime
        /// of the application. The StackExchange client should handle most reconnection issues, but we can force a reconnection if it
        /// holds open to long 
        /// </summary>
        public void Dispose()
        {
             ReleaseUnmanagedResources();
             GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Move the consumer pointer along, so that we grab the next record
        /// </summary>
        /// <param name="message"></param>
        public void Acknowledge(Message message)
        {
            _logger.Value.InfoFormat("RmqMessageConsumer: Acknowledging message {0}", message.Id.ToString());
            _db.StreamAcknowledge(_queueName, _consumerGroup, message.)
        }


        /// <summary>
        /// Clear the queue
        /// </summary>
        public void Purge()
        {
            using (var client = Pool.Value.GetClient())
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
        public Message[] Receive(int timeoutInMilliseconds)
        {
            _logger.Value.DebugFormat("RedisStreamsConsumer: Preparing to retrieve next message from queue {0} with routing key {1} via exchange {2} on connection {3}", _queueName, Topic);

            try
            {
                var entries = _db.StreamReadGroup(_queueName, _consumerGroup, _consumerId, ">", _batchSize);
                var messageCreator = new RedisStreamsMessageCreator();
                return entries.Select(se => messageCreator.CreateMessage(se)).ToArray();
            }
            catch (Exception e)    //TODO: What exceptions can be thrown, and what can we do with them
            {
                throw new ChannelFailureException(string.Format("Could not read from Redis"));
            }
            return new Message[0];
        }


        /// <summary>
        /// This a 'do nothing operation' as we have already popped
        /// </summary>
        /// <param name="message"></param>
        /// <param name="requeue"></param>
        public void Reject(Message message, bool requeue)
        {
            _inflight.Remove(message.Id);
        }

        /// <summary>
        /// Push the Id back onto the queue, to re-order
        /// </summary>
        /// <param name="message"></param>
        public void Requeue(Message message)
        {
            message.Header.HandledCount++;
            using (var client = Pool.Value.GetClient())
            {
                if (_inflight.ContainsKey(message.Id))
                {
                    var msgId = _inflight[message.Id];
                    client.AddItemToList(_queueName, msgId);
                    var redisMsg = CreateMessage(message);
                    StoreMessage(client, redisMsg, long.Parse(msgId));
                    _inflight.Remove(message.Id);
                }
                else
                {
                    throw new ChannelFailureException(string.Format("Expected to find message id {0} in-flight but was not", message.Id.ToString()));
                }
            }
        }

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delayMilliseconds">Number of milliseconds to delay delivery of the message.</param>
        public void Requeue(Message message, int delayMilliseconds)
        {
            Task.Delay(delayMilliseconds).Wait();
            message.Header.DelayedMilliseconds = delayMilliseconds;
            Requeue(message);
        }
        
        private void ReleaseUnmanagedResources()
        {
            throw new NotImplementedException();
        }
   }
}
