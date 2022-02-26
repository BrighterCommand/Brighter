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
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using StackExchange.Redis;

namespace Paramore.Brighter.MessagingGateway.RedisStreams
{
    internal enum ReadState
    {
        Pending, 
        New
    }
    
    
    public class RedisStreamsConsumer : RedisStreamsGateway, IAmAMessageConsumer
    {
        private readonly RedisValue NEW_MESSAGES = new RedisValue(">");
        private readonly RedisValue PENDING_MESSAGES = new RedisValue("0-0");

        /* see RedisMessageProducer to understand how we are using a dynamic recipient list model with Redis */

        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RedisStreamsConsumer>();
        private readonly string _queueName;
        private readonly string _consumerGroup;
        private readonly int _batchSize;
        private readonly string _consumerId;
        private ReadState _readState;
        private readonly IDatabase _db;

        /// <summary>
        /// Creates a consumer that reads from a List in Redis via a BLPOP (so will block).
        /// </summary>
        /// <param name="redisStreamsConfiguration">Configuration for our Redis client etc.</param>
        /// <param name="queueName">Key of the list in Redis we want to read from</param>
        public RedisStreamsConsumer(RedisStreamsConfiguration redisStreamsConfiguration, string queueName)
            : base(redisStreamsConfiguration)
        {
            _batchSize = redisStreamsConfiguration.BatchSize;
            _consumerGroup = redisStreamsConfiguration.ConsumerGroup;
            _consumerId = redisStreamsConfiguration.ConsumerId;
            _queueName = queueName;
            _readState = ReadState.Pending;
            
            _db = Redis.GetDatabase();

            //if we can't create the group, fail fast (RAII)
            if (!_db.StreamCreateConsumerGroup(queueName, redisStreamsConfiguration.ConsumerGroup, StreamPosition.NewMessages))
            {
                throw new InvalidOperationException($"Not able to create the consumer group for consumer: {_consumerId}");
            }
        }

        /// <summary>
        /// Free up our Redis connection. Connections are relatively expensive, so the Consumer can be kept on open for the lifetime
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
            var redisId = message.Header.Bag[MessageNames.REDIS_ID].ToString();
            s_logger.LogInformation("RmqMessageConsumer: Acknowledging message {RedisId}", redisId);
            _db.StreamAcknowledge(_queueName, _consumerGroup, new RedisValue(redisId));
        }

        /// <summary>
        /// Clear the queue
        /// </summary>
        public void Purge()
        {
            s_logger.LogDebug("RmqMessageConsumer: Purging channel {QueueName}", _queueName);
        }

        /// <summary>
        /// Get the next message off the Redis list, within a timeout
        /// It is worth understanding that we have different options when reading streams
        /// Read pending messages for this consumer group i.e. not acknowledged, perhaps following a consumer failure
        /// Read new messages for this consumer i.e. not pending
        /// This means what we have to switch modes. At startup we always read pending messages. Once we have no more pending messages,
        /// we begin reading outstanding messages 
        /// </summary>
        /// <param name="timeoutInMilliseconds">The period to await a message</param>
        /// <returns>The message read from the list</returns>
        public Message[] Receive(int timeoutInMilliseconds)
        {
            s_logger.LogDebug("RedisStreamsConsumer: Preparing to retrieve next message from queue {QueueName}", _queueName);
            
            //TODO: We need to read pending messages first, then we can start to consumer new messages

            try
            {
                var entries = _db.StreamReadGroup(_queueName, _consumerGroup, _consumerId, NEW_MESSAGES, _batchSize);
                var messageCreator = new RedisStreamsMessageCreator();
                return entries.Select(se => messageCreator.CreateMessage(se)).ToArray();
            }
            catch (Exception e)    //TODO: What exceptions can be thrown, and what can we do with them
            {
                throw new ChannelFailureException("Could not read from Redis");
            }
            return new Message[0];
        }


        /// <summary>
        /// In a stream we can't delete, so we just skip over a record we don't intend to process so it won't get reprocessed
        /// This amounts to an acknowledge if requeue is false, so its the same operation on a stream. However, if you configure a DLQ, we will push onto
        /// the DLQ if requeue is false.
        /// </summary>
        /// <param name="message">The message to reject</param>
        /// <param name="requeue">Should we requeue (do nothing), or not (skip and add to DLQ)</param>
        public void Reject(Message message, bool requeue)
        {
            if (requeue)
            {
                Acknowledge(message);
                //TODO: Put on DLQ if configured
                return;
            }
            
            Requeue(message);
        }
        
        /// <summary>
        /// Call reject with false for requeue i.e. skip past this record by doing an ack and put on DLQ if configured
        /// </summary>
        /// <param name="message">The message you are rejecting</param>
        public void Reject(Message message)
        {
            Reject(message, false);
        }


        /// <summary>
        /// Push the Id back onto the queue. In the case of a stream this simply means we don't acknowledge, so it will be processed again
        /// </summary>
        /// <param name="message"></param>
        public void Requeue(Message message)
        {
            return;
        }

        /// <summary>
        /// Re-queues the specified message.
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
