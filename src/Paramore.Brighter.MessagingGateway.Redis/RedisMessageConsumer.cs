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
using ServiceStack.Redis;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class RedisMessageConsumer : RedisMessageGateway, IAmAMessageConsumer
    {
        
        /* see RedisMessageProducer to understand how we are using a dynamic recipient list model with Redis */

        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<RedisMessageConsumer>);
        private const string QUEUES = "queues";
        
        private readonly string _queueName;
        
        private readonly Dictionary<Guid, string> _inflight = new Dictionary<Guid, string>();
 
        /// <summary>
        /// Creates a consumer that reads from a List in Redis via a BLPOP (so will block).
        /// </summary>
        /// <param name="redisMessagingGatewayConfiguration">Configuration for our Redis cient etc.</param>
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
            _logger.Value.InfoFormat("RmqMessageConsumer: Acknowledging message {0}", message.Id.ToString());
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
            _logger.Value.DebugFormat("RedisMessageConsumer: Preparing to retrieve next message from queue {0} with routing key {1} via exchange {2} on connection {3}", _queueName, Topic);

            if (_inflight.Any())
            {
                 _logger.Value.ErrorFormat("RedisMessageConsumer: Preparing to retrieve next message from queue {0}, but have unacked or not rejected message ", _queueName);
                throw new ChannelFailureException(string.Format("Unacked message still in flight with id: {0}", _inflight.Keys.First().ToString()));   
            }
            
            var message = new Message();
            IRedisClient client = null;
            try
            {
                client = GetClient();
                EnsureConnection(client);
                (string msgId, string rawMsg) redisMessage = ReadMessage(client, timeoutInMilliseconds);
                message = new RedisMessageCreator().CreateMessage(redisMessage.rawMsg);
                
                if (message.Header.MessageType != MessageType.MT_NONE && message.Header.MessageType != MessageType.MT_UNACCEPTABLE)
                {
                    _inflight.Add(message.Id, redisMessage.msgId);
                }
            }
            catch (TimeoutException te)
            {
                _logger.Value.ErrorFormat("Could not connect to Redis client within {0} milliseconds", timeoutInMilliseconds.ToString());
                throw new ChannelFailureException(
                    string.Format("Could not connect to Redis client within {0} milliseconds", timeoutInMilliseconds.ToString()),
                    te
                );
            }
            catch (RedisException re)
            {
                _logger.Value.ErrorFormat($"Could not connect to Redis: {re.Message}");
                throw new ChannelFailureException(string.Format("Could not connect to Redis client - see inner exception for details" ), re);
                 
            }
            finally
            {
                client?.Dispose();
            }
            return new Message[] {message};
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
                    var redisMsg = CreateRedisMessage(message);
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
        
        /*Virtual to allow testing to simulate client failure*/
        protected virtual IRedisClient GetClient()
        {
            return Pool.Value.GetClient();
        }


        private void EnsureConnection(IRedisClient client)
        {
            _logger.Value.DebugFormat("RedisMessagingGateway: Creating queue {0}", _queueName);
            //what is the queue list key
            var key = Topic + "." + QUEUES;
            //subscribe us 
            client.AddItemToSet(key, _queueName);

        }

        private (string msgId, string rawMsg) ReadMessage(IRedisClient client, int timeoutInMilliseconds)
        {
            var msg = string.Empty;
            var latestId = client.BlockingRemoveStartFromList(_queueName, TimeSpan.FromMilliseconds(timeoutInMilliseconds));
            if (latestId != null)
            {
                var key = Topic + "." + latestId;
                msg = client.GetValue(key);
                _logger.Value.InfoFormat(
                    "Redis: Received message from queue {0} with routing key {0}, message: {1}",
                    _queueName, Topic, JsonConvert.SerializeObject(msg), Environment.NewLine);
            }
            else
            {
               _logger.Value.DebugFormat(
                   "RmqMessageConsumer: Time out without receiving message from queue {0} with routing key {1}",
                    _queueName, Topic);
  
            }
            return (latestId, msg);
        }
        
    }
}
