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
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Logging;
using ServiceStack.Redis;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    public partial class RedisMessageConsumer : RedisMessageGateway, IAmAMessageConsumerSync, IAmAMessageConsumerAsync
    {
        
        /* see RedisMessageProducer to understand how we are using a dynamic recipient list model with Redis */

        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RedisMessageConsumer>();
        private const string QUEUES = "queues";
        
        private readonly ChannelName _queueName;
        
        private readonly Dictionary<string, string> _inflight = new();
 
        /// <summary>
        /// Creates a consumer that reads from a List in Redis via a BLPOP (so will block).
        /// </summary>
        /// <param name="redisMessagingGatewayConfiguration">Configuration for our Redis client etc.</param>
        /// <param name="queueName">Key of the list in Redis we want to read from</param>
        /// <param name="topic">The topic that the list subscribes to</param>
        public RedisMessageConsumer(
            RedisMessagingGatewayConfiguration redisMessagingGatewayConfiguration, 
            ChannelName queueName, 
            RoutingKey topic)
            :base(redisMessagingGatewayConfiguration, topic)
        {
            _queueName = queueName;
       }

        /// <summary>
        /// Acknowledge the message, removing it from the queue 
        /// </summary>
        /// <remarks>
        /// This a 'do nothing operation' as with Redis we pop the message from the queue to read;
        /// this allows us to have competing consumers, and thus a message is always 'consumed' even
        /// if we fail to process it.
        /// The risk with Redis is that we lose any in-flight message if we kill the service, without allowing
        /// the job to run to completion. Brighter uses run to completion if shut down properly, but not if you
        /// just kill the process.
        /// If you need the level of reliability that unprocessed messages that return to the queue don't use Redis.
        /// </remarks>
        /// <param name="message"></param>
        public void Acknowledge(Message message)
        {
            Log.AcknowledgingMessage(s_logger, message.Id);
            _inflight.Remove(message.Id);
        }
        
        /// <summary>
        /// Acknowledge the message, removing it from the queue 
        /// </summary>
        /// <remarks>
        /// This a 'do nothing operation' as with Redis we pop the message from the queue to read;
        /// this allows us to have competing consumers, and thus a message is always 'consumed' even
        /// if we fail to process it.
        /// The risk with Redis is that we lose any in-flight message if we kill the service, without allowing
        /// the job to run to completion. Brighter uses run to completion if shut down properly, but not if you
        /// just kill the process.
        /// If you need the level of reliability that unprocessed messages that return to the queue don't use Redis.
        /// This is async over sync as the underlying operation does not block
        /// </remarks>
        /// <param name="message"></param>
        public Task AcknowledgeAsync(Message message, CancellationToken cancellationToken = default(CancellationToken))
        {
            Acknowledge(message);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Dispose of the Redis consumer
        /// </summary>
        /// <remarks>
        /// Free up our RedisMangerPool, connections not held open between invocations of Receive, so you can create
        /// a consumer and keep it for program lifetime, disposing at the end only, without fear of a leak
        /// </remarks> 
        public void Dispose()
        {
            DisposePool();
            GC.SuppressFinalize(this);
        }
        

        /// <inheritdoc cref="IAsyncDisposable"/> 
        public async ValueTask DisposeAsync()
        {
            await DisposePoolAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this); 
        }
        /// <summary>
        /// Clear the queue
        /// </summary>
        public void Purge()
        {
            Log.PurgingChannel(s_logger, _queueName);
            
            using var client = GetClient();
            if (client == null)
                throw new ChannelFailureException("RedisMessagingGateway: No Redis client available");
            
            //This kills the queue, not the messages, which we assume expire
            client.RemoveAllFromList(_queueName);
        }
        
        /// <summary>
        /// Clear the queue
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        public async Task PurgeAsync(CancellationToken cancellationToken = default(CancellationToken))
        { 
            Log.PurgingChannel(s_logger, _queueName);
            
            var client = await GetClientAsync(cancellationToken);
            if (client == null)
                throw new ChannelFailureException("RedisMessagingGateway: No Redis client available");
            
            //This kills the queue, not the messages, which we assume expire
            await client.RemoveAllFromListAsync(_queueName, token: cancellationToken);
        }

        /// <summary>
        /// Get the next message off the Redis list, within a timeout
        /// </summary>
        /// <param name="timeOut">The period to await a message. Defaults to 1s.</param>
        /// <returns>The message read from the list</returns>
        public Message[] Receive(TimeSpan? timeOut = null)
        {
            Log.RetrievingNextMessage(s_logger, _queueName, Topic);

            if (_inflight.Any())
            {
                Log.UnackedMessageInFlight(s_logger, _queueName);
                throw new ChannelFailureException($"Unacked message still in flight with id: {_inflight.Keys.First()}");   
            }
            
            if (timeOut == null || timeOut.GetValueOrDefault().TotalSeconds < 1)
            {
                timeOut = TimeSpan.FromSeconds(1);
            }
            
            try
            {
                var client = GetClient();
                if (client == null)
                    throw new ChannelFailureException("RedisMessagingGateway: No Redis client available");
                
                EnsureConnection(client);
                (string? msgId, string rawMsg) redisMessage = ReadMessage(client, timeOut.Value);
                if (redisMessage.msgId == null || string.IsNullOrEmpty(redisMessage.rawMsg))
                    return [];
                
                var message = new RedisMessageCreator().CreateMessage(redisMessage.rawMsg);
                if (message.Header.MessageType != MessageType.MT_NONE && message.Header.MessageType != MessageType.MT_UNACCEPTABLE)
                {
                    _inflight.Add(message.Id, redisMessage.msgId);
                }
                
                return [message];
            }
            catch (TimeoutException te)
            {
                Log.CouldNotConnectToRedisClient(s_logger, timeOut.Value.TotalMilliseconds.ToString(CultureInfo.CurrentCulture));
                throw new ChannelFailureException($"Could not connect to Redis client within {timeOut.Value.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)} milliseconds", te);
            }
            catch (RedisException re)
            {
                Log.CouldNotConnectToRedis(s_logger, re.Message);
                throw new ChannelFailureException("Could not connect to Redis client - see inner exception for details", re);
            }
        }

        /// <summary>
        /// Get the next message off the Redis list, within a timeout
        /// </summary>
        /// <param name="timeOut">The period to await a message. Defaults to 1s.</param>
        /// <param name="cancellationToken">Cancel the receive operation</param>
        /// <returns>The message read from the list</returns>
        public async Task<Message[]> ReceiveAsync(TimeSpan? timeOut = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            Log.RetrievingNextMessage(s_logger, _queueName, Topic);

            if (_inflight.Any())
            {
                Log.UnackedMessageInFlight(s_logger, _queueName);
                throw new ChannelFailureException($"Unacked message still in flight with id: {_inflight.Keys.First()}");   
            }

            timeOut ??= TimeSpan.FromSeconds(1);
            try
            {
                await using IRedisClientAsync? client = await GetClientAsync(cancellationToken);
                if (client == null)
                    throw new ChannelFailureException("RedisMessagingGateway: No Redis client available");
                
                await EnsureConnectionAsync(client);
                (string? msgId, string rawMsg) redisMessage = await ReadMessageAsync(client, timeOut.Value);
                if (redisMessage.msgId == null || string.IsNullOrEmpty(redisMessage.rawMsg))
                    return [];
                
                var message = new RedisMessageCreator().CreateMessage(redisMessage.rawMsg);
                
                if (message.Header.MessageType != MessageType.MT_NONE && message.Header.MessageType != MessageType.MT_UNACCEPTABLE)
                {
                    _inflight.Add(message.Id, redisMessage.msgId);
                }
                
                return [message];
            }
            catch (TimeoutException te)
            {
                Log.CouldNotConnectToRedisClient(s_logger, timeOut.Value.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));
                throw new ChannelFailureException($"Could not connect to Redis client within {timeOut.Value.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)} milliseconds", te);
            }
            catch (RedisException re)
            {
                Log.CouldNotConnectToRedis(s_logger, re.Message);
                throw new ChannelFailureException("Could not connect to Redis client - see inner exception for details", re);
            }
        }

        /// <summary>
        /// This a 'do nothing operation' as we have already popped
        /// </summary>
        /// <param name="message">The message to reject</param>
        public bool Reject(Message message)
        {
            _inflight.Remove(message.Id);
            return true;
        }

        /// <summary>
        /// This a 'do nothing operation' as we have already popped
        /// </summary>
        /// <param name="message">The message to reject</param>
        /// <param name="cancellationToken">The cancellation token</param>
        public Task<bool> RejectAsync(Message message, CancellationToken cancellationToken = default(CancellationToken))
            => Task.FromResult(Reject(message));

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delay">Delay is not supported</param>
        /// <returns>True if the message was requeued</returns>
         public bool Requeue(Message message, TimeSpan? delay = null)
        {
           message.Header.HandledCount++;
            using var client = GetClient();
            if (client == null)
                throw new ChannelFailureException("RedisMessagingGateway: No Redis client available");
            
            //TODO: we removed delay support here because it blocked the pump
            // Return to this once we have scheduled message support
            
            if (_inflight.TryGetValue(message.Id, out string? msgId))
            {
                client.AddItemToList(_queueName, msgId);
                var redisMsg = CreateRedisMessage(message);
                StoreMessage(client, redisMsg, long.Parse(msgId));
                _inflight.Remove(message.Id);
                return true;
            }
            else
            {
                Log.MessageNotFoundInFlight(s_logger, message.Id);
                return false;
            }
        }

        /// <summary>
        /// Requeues the specified message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delay">Delay is not supported</param>
        /// <param name="cancellationToken">Cancel the requeue operation</param>
        /// <returns>True if the message was requeued</returns>
        public async Task<bool> RequeueAsync(Message message, TimeSpan? delay = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            message.Header.HandledCount++;
            var client = await GetClientAsync(cancellationToken);
            if (client == null)
                throw new ChannelFailureException("RedisMessagingGateway: No Redis client available");
            
            //TODO: we removed delay support here because it blocked the pump
            // Return to this once we have scheduled message support
            
            if (_inflight.TryGetValue(message.Id, out string? msgId))
            {
                await client.AddItemToListAsync(_queueName, msgId, cancellationToken);
                var redisMsg = CreateRedisMessage(message);
                await StoreMessageAsync(client, redisMsg, long.Parse(msgId));
                _inflight.Remove(message.Id);
                return true;
            }
            else
            {
                Log.MessageNotFoundInFlight(s_logger, message.Id);
                return false;
            } 
        }
        
        // Virtual to allow testing to simulate client failure
        protected virtual IRedisClient? GetClient()
        {
            if (s_pool == null)
                throw new ChannelFailureException("RedisMessagingGateway: No connection pool available");

            try
            {
                return s_pool.Value.GetClient();
            }
            catch (TimeoutException te)
            {
                throw new ChannelFailureException("RedisMessagingGateway: Timeout on getting client from pool", te);
            }
            catch(RedisException re)
            {
                throw new ChannelFailureException("RedisMessagingGateway: Error on getting client from pool", re);
            }
        }

        // Virtual to allow testing to simulate client failure
        protected virtual async Task<IRedisClientAsync?> GetClientAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (s_pool == null)
                throw new ChannelFailureException("RedisMessagingGateway: No connection pool available");

            try
            {
                return await s_pool.Value.GetClientAsync(cancellationToken);
            }
            catch (TimeoutException te)
            {
                throw new ChannelFailureException("RedisMessagingGateway: Timeout on getting client from pool", te);
            }
            catch(RedisException re)
            {
                throw new ChannelFailureException("RedisMessagingGateway: Error on getting client from pool", re);
            }
        }
            
        private void EnsureConnection(IRedisClient client)
        {
            Log.CreatingQueue(s_logger, _queueName);
            //what is the queue list key
            var key = Topic + "." + QUEUES;
            //subscribe us 
            client.AddItemToSet(key, _queueName);
        }
        
        private async Task EnsureConnectionAsync(IRedisClientAsync client)
        {
            Log.CreatingQueue(s_logger, _queueName);
            //what is the queue list key
            var key = Topic + "." + QUEUES;
            //subscribe us 
            await client.AddItemToSetAsync(key, _queueName);
        }

        private (string? msgId, string rawMsg) ReadMessage(IRedisClient client, TimeSpan timeOut)
        {
            var msg = string.Empty;
            var latestId = client.BlockingRemoveStartFromList(_queueName, timeOut);
            if (latestId != null)
            {
                var key = Topic + "." + latestId;
                msg = client.GetValue(key);
                Log.ReceivedMessageFromQueue(s_logger, _queueName, Topic, JsonSerializer.Serialize(msg, JsonSerialisationOptions.Options));
            }
            else
            {
                Log.TimeoutWithoutReceivingMessage(s_logger, _queueName, Topic);
            }
            return (latestId, msg);
        }
        
        private async Task<(string? msgId, string rawMsg)> ReadMessageAsync(IRedisClientAsync client, TimeSpan timeOut)
        {
            var msg = string.Empty;
            string? latestId = null;
            try
            {
                using var cts = new CancellationTokenSource(timeOut);
                latestId = await client.BlockingRemoveStartFromListAsync(_queueName, timeOut, cts.Token);
                if (latestId != null)
                {
                    var key = Topic + "." + latestId;
                    msg = await client.GetValueAsync(key);
                    Log.ReceivedMessageFromQueue(s_logger, _queueName, Topic, JsonSerializer.Serialize(msg, JsonSerialisationOptions.Options));
                }
            }
            catch (OperationCanceledException)
            {
                Log.TimeoutWithoutReceivingMessage(s_logger, _queueName, Topic);
            }
            catch (RedisException re) when (re.InnerException is OperationCanceledException)
            {
                Log.TimeoutWithoutReceivingMessage(s_logger, _queueName, Topic);
            }
            
            return (latestId, msg);
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Information, "RmqMessageConsumer: Acknowledging message {Id}")]
            public static partial void AcknowledgingMessage(ILogger logger, string id);

            [LoggerMessage(LogLevel.Debug, "RmqMessageConsumer: Purging channel {ChannelName}")]
            public static partial void PurgingChannel(ILogger logger, ChannelName channelName);
            
            [LoggerMessage(LogLevel.Debug, "RedisMessageConsumer: Preparing to retrieve next message from queue {ChannelName} with routing key {Topic}")]
            public static partial void RetrievingNextMessage(ILogger logger, ChannelName channelName, RoutingKey topic);
            
            [LoggerMessage(LogLevel.Error, "RedisMessageConsumer: Preparing to retrieve next message from queue {ChannelName}, but have unacked or not rejected message")]
            public static partial void UnackedMessageInFlight(ILogger logger, ChannelName channelName);
            
            [LoggerMessage(LogLevel.Error, "Could not connect to Redis client within {Timeout} milliseconds")]
            public static partial void CouldNotConnectToRedisClient(ILogger logger, string timeout);
            
            [LoggerMessage(LogLevel.Error, "Could not connect to Redis: {ErrorMessage}")]
            public static partial void CouldNotConnectToRedis(ILogger logger, string errorMessage);
            
            [LoggerMessage(LogLevel.Debug, "RedisMessagingGateway: Creating queue {ChannelName}")]
            public static partial void CreatingQueue(ILogger logger, ChannelName channelName);
            
            [LoggerMessage(LogLevel.Information, "Redis: Received message from queue {ChannelName} with routing key {Topic}, message: {Request}")]
            public static partial void ReceivedMessageFromQueue(ILogger logger, ChannelName channelName, RoutingKey topic, string request);
            
            [LoggerMessage(LogLevel.Debug, "RmqMessageConsumer: Time out without receiving message from queue {ChannelName} with routing key {Topic}")]
            public static partial void TimeoutWithoutReceivingMessage(ILogger logger, ChannelName channelName, RoutingKey topic);
            
            [LoggerMessage(LogLevel.Error, "Expected to find message id {MessageId} in-flight but was not")]
            public static partial void MessageNotFoundInFlight(ILogger logger, string messageId);
        }
    }
}

