﻿#region Licence
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
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using ServiceStack.Redis;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessageConsumer : RedisMessageGateway, IAmAMessageConsumer
    {
        
        /* see RedisMessageProducer to understand how we are using a dynamic recipient list model with Redis */

        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RedisMessageConsumer>();
        private const string QUEUES = "queues";
        
        private readonly string _queueName;
        
        private readonly Dictionary<string, string> _inflight = new Dictionary<string, string>();
 
        /// <summary>
        /// Creates a consumer that reads from a List in Redis via a BLPOP (so will block).
        /// </summary>
        /// <param name="redisMessagingGatewayConfiguration">Configuration for our Redis client etc.</param>
        /// <param name="queueName">Key of the list in Redis we want to read from</param>
        /// <param name="topic">The topic that the list subscribes to</param>
        public RedisMessageConsumer(
            RedisMessagingGatewayConfiguration redisMessagingGatewayConfiguration, 
            string queueName, 
            string topic)
            :base(redisMessagingGatewayConfiguration)
        {
            _queueName = queueName;
            Topic = topic;
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
            s_logger.LogInformation("RmqMessageConsumer: Acknowledging message {Id}", message.Id);
            _inflight.Remove(message.Id);
        }

        /// <summary>
        /// Free up our RedisMangerPool, connections not held open between invocations of Receive, so you can create
        /// a consumer and keep it for program lifetime, disposing at the end only, without fear of a leak
        /// </summary>
        public void Dispose()
        {
            DisposePool();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Clear the queue
        /// </summary>
        public void Purge()
        {
            using var client = Pool.Value.GetClient();
            s_logger.LogDebug("RmqMessageConsumer: Purging channel {ChannelName}", _queueName);
            //This kills the queue, not the messages, which we assume expire
            client.RemoveAllFromList(_queueName);
        }

        /// <summary>
        /// Get the next message off the Redis list, within a timeout
        /// </summary>
        /// <param name="timeOut">The period to await a message. Defaults to 300ms.</param>
        /// <returns>The message read from the list</returns>
        public Message[] Receive(TimeSpan? timeOut = null)
        {
            s_logger.LogDebug("RedisMessageConsumer: Preparing to retrieve next message from queue {ChannelName} with routing key {Topic}", _queueName, Topic);

            if (_inflight.Any())
            {
                 s_logger.LogError("RedisMessageConsumer: Preparing to retrieve next message from queue {ChannelName}, but have unacked or not rejected message", _queueName);
                throw new ChannelFailureException($"Unacked message still in flight with id: {_inflight.Keys.First()}");   
            }
            
            Message message;
            IRedisClient client = null;
            timeOut ??= TimeSpan.FromMilliseconds(300);
            try
            {
                client = GetClient();
                EnsureConnection(client);
                (string msgId, string rawMsg) redisMessage = ReadMessage(client, timeOut.Value);
                message = new RedisMessageCreator().CreateMessage(redisMessage.rawMsg);
                
                if (message.Header.MessageType != MessageType.MT_NONE && message.Header.MessageType != MessageType.MT_UNACCEPTABLE)
                {
                    _inflight.Add(message.Id, redisMessage.msgId);
                }
            }
            catch (TimeoutException te)
            {
                s_logger.LogError("Could not connect to Redis client within {Timeout} milliseconds", timeOut.Value.TotalMilliseconds.ToString());
                throw new ChannelFailureException($"Could not connect to Redis client within {timeOut.Value.TotalMilliseconds.ToString()} milliseconds", te);
            }
            catch (RedisException re)
            {
                s_logger.LogError("Could not connect to Redis: {ErrorMessage}", re.Message);
                throw new ChannelFailureException("Could not connect to Redis client - see inner exception for details", re);
                 
            }
            finally
            {
                client?.Dispose();
            }
            return [message];
        }


        /// <summary>
        /// This a 'do nothing operation' as we have already popped
        /// </summary>
        /// <param name="message"></param>
        public void Reject(Message message)
        {
            _inflight.Remove(message.Id);
        }

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delay"> Time to delay delivery of the message. 0 is no delay. Defaults to 0</param>
        /// <returns>True if the message was requeued</returns>
         public bool Requeue(Message message, TimeSpan? delay = null)
        {
           delay ??= TimeSpan.Zero;

           //TODO: This blocks. We should use a thread to repost to the queue after n milliseconds, using a Timer
           if (delay > TimeSpan.Zero)
           {
               Task.Delay(delay.Value).Wait();
               message.Header.Delayed = delay.Value;
           }

           message.Header.HandledCount++;
            using var client = Pool.Value.GetClient();
            if (_inflight.ContainsKey(message.Id))
            {
                var msgId = _inflight[message.Id];
                client.AddItemToList(_queueName, msgId);
                var redisMsg = CreateRedisMessage(message);
                StoreMessage(client, redisMsg, long.Parse(msgId));
                _inflight.Remove(message.Id);
                return true;
            }
            else
            {
                s_logger.LogError(string.Format("Expected to find message id {0} in-flight but was not", message.Id));
                return false;
            }
        }
        
        /*Virtual to allow testing to simulate client failure*/
        protected virtual IRedisClient GetClient()
        {
            return Pool.Value.GetClient();
        }


        private void EnsureConnection(IRedisClient client)
        {
            s_logger.LogDebug("RedisMessagingGateway: Creating queue {ChannelName}", _queueName);
            //what is the queue list key
            var key = Topic + "." + QUEUES;
            //subscribe us 
            client.AddItemToSet(key, _queueName);

        }

        private (string msgId, string rawMsg) ReadMessage(IRedisClient client, TimeSpan timeOut)
        {
            var msg = string.Empty;
            var latestId = client.BlockingRemoveStartFromList(_queueName, timeOut);
            if (latestId != null)
            {
                var key = Topic + "." + latestId;
                msg = client.GetValue(key);
                s_logger.LogInformation(
                    "Redis: Received message from queue {ChannelName} with routing key {Topic}, message: {Request}",
                    _queueName, Topic, JsonSerializer.Serialize(msg, JsonSerialisationOptions.Options));
            }
            else
            {
               s_logger.LogDebug(
                   "RmqMessageConsumer: Time out without receiving message from queue {ChannelName} with routing key {Topic}",
                    _queueName, Topic);
  
            }
            return (latestId, msg);
        }
        
    }
}
