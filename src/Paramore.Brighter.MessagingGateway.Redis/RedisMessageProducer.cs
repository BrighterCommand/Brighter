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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using ServiceStack.Redis;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    /*Why don't we simply use Redis Pub-Sub here?
     We don't want to use pub-sub because you can't support competing consumers and messages 'disappear'
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
         
    */

    public partial class RedisMessageProducer(
        RedisMessagingGatewayConfiguration redisMessagingGatewayConfiguration,
        RedisMessagePublication publication)
        : RedisMessageGateway(redisMessagingGatewayConfiguration, publication.Topic!), IAmAMessageProducerSync, IAmAMessageProducerAsync
    {

        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RedisMessageProducer>();
        private Publication _publication = publication; 
        private const string NEXT_ID = "nextid";
        private const string QUEUES = "queues";

        /// <summary>
        /// The publication configuration for this producer
        /// </summary>
        public Publication Publication
        {
            get { return _publication; }
            set {_publication = value;}
        }

        /// <inheritdoc />
        public Activity? Span { get; set; }
        
        /// <inheritdoc />
        public IAmAMessageScheduler? Scheduler { get; set; }

        public void Dispose()
        {
            DisposePool();
            GC.SuppressFinalize(this);
        }
        
        public async ValueTask DisposeAsync()
        {
            await DisposePoolAsync();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Task.</returns>
        public void Send(Message message) => SendWithDelay(message, TimeSpan.Zero);

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="cancellationToken">A token to cancel the send operation</param>
        /// <returns>Task.</returns>
        public async Task SendAsync(Message message, CancellationToken cancellationToken = default)
        {
            await SendWithDelayAsync(message, TimeSpan.Zero, cancellationToken);
        }
        
        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <remarks>
        /// No delay support on Redis
        /// </remarks>
        /// <param name="message">The message.</param>
        /// <param name="delay">The sending delay</param>
        /// <returns>Task.</returns>
        public void SendWithDelay(Message message, TimeSpan? delay = null)
        {
            if (s_pool is null)
            {
                throw new ChannelFailureException("RedisMessageProducer: Connection pool has not been initialized");
            }
            
            delay ??= TimeSpan.Zero;
            if (delay != TimeSpan.Zero)
            {
                var schedulerSync = (IAmAMessageSchedulerSync)Scheduler!;
                schedulerSync.Schedule(message, delay.Value);
                return;
            }
           
            using var client = s_pool.Value.GetClient();
            Topic = message.Header.Topic;

            Log.PreparingToSend(s_logger);
  
            var redisMessage = CreateRedisMessage(message);

            Log.PublishingMessage(s_logger, message.Header.Topic, message.Id.ToString(), message.Body.Value);
            //increment a counter to get the next message id
            var nextMsgId = IncrementMessageCounter(client);
            //store the message, against that id
            StoreMessage(client, redisMessage, nextMsgId);
            //If there are subscriber queues, push the message to the subscriber queues
            var pushedTo = PushToQueues(client, nextMsgId);
            Log.PublishedMessage(s_logger, message.Header.Topic, message.Id.ToString(), message.Body.Value, string.Join(", ", pushedTo));
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        ///  <remarks>
        /// No delay support on Redis
        /// </remarks>
        /// <param name="message">The message.</param>
        /// <param name="delay">The sending delay</param>
        /// <param name="cancellationToken"></param>
        /// <returns>Task.</returns>
        public async Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default)
        {
            if (s_pool is null)
            {
                throw new ChannelFailureException("RedisMessageProducer: Connection pool has not been initialized");
            }
            
            delay ??= TimeSpan.Zero;
            if (delay != TimeSpan.Zero)
            {
                var schedulerAsync = (IAmAMessageSchedulerAsync)Scheduler!;
                await schedulerAsync.ScheduleAsync(message, delay.Value, cancellationToken);
                return;
            }

            await using var client = await s_pool.Value.GetClientAsync(token: cancellationToken);
            Topic = message.Header.Topic;

            Log.PreparingToSend(s_logger);
  
            var redisMessage = CreateRedisMessage(message);

            Log.PublishingMessage(s_logger, message.Header.Topic, message.Id.ToString(), message.Body.Value);
            //increment a counter to get the next message id
            var nextMsgId = await IncrementMessageCounterAsync(client, cancellationToken);
            //store the message, against that id
            await StoreMessageAsync(client, redisMessage, nextMsgId);
            //If there are subscriber queues, push the message to the subscriber queues
            var pushedTo = await PushToQueuesAsync(client, nextMsgId, cancellationToken);
            Log.PublishedMessage(s_logger, message.Header.Topic, message.Id.ToString(), message.Body.Value, string.Join(", ", pushedTo));
        }

        private HashSet<string> PushToQueues(IRedisClient client, long nextMsgId)
        {
            var key = Topic + "." + QUEUES;
            var queues = client.GetAllItemsFromSet(key);
            foreach (var queue in queues)
            {
                //First add to the queue itself
                client.AddItemToList(queue, nextMsgId.ToString());
            }
            return queues;
        }
        
        private async Task<HashSet<string>> PushToQueuesAsync(IRedisClientAsync client, long nextMsgId, CancellationToken cancellationToken = default)
        {
            var key = Topic + "." + QUEUES;
            var queues = await client.GetAllItemsFromSetAsync(key, cancellationToken);
            foreach (var queue in queues)
            {
                //First add to the queue itself
                await client.AddItemToListAsync(queue, nextMsgId.ToString(), cancellationToken);
            }
            return queues;
        }

        private long IncrementMessageCounter(IRedisClient client)
        {
            //This holds the next id for this topic; we use that to store message contents and signal to queue
            //that there is a message to read.
            var key = Topic + "." + NEXT_ID;
            return client.IncrementValue(key);
        }
        
        private async Task<long> IncrementMessageCounterAsync(IRedisClientAsync client, CancellationToken cancellationToken = default)
        {
            //This holds the next id for this topic; we use that to store message contents and signal to queue
            //that there is a message to read.
            var key = Topic + "." + NEXT_ID;
            return await client.IncrementValueAsync(key, cancellationToken);
        }

        private static partial class Log
        {
            [LoggerMessage(LogLevel.Debug, "RedisMessageProducer: Preparing to send message")]
            public static partial void PreparingToSend(ILogger logger);

            [LoggerMessage(LogLevel.Debug, "RedisMessageProducer: Publishing message with topic {Topic} and id {Id} and body: {Request}")]
            public static partial void PublishingMessage(ILogger logger, string topic, string id, string request);
            
            [LoggerMessage(LogLevel.Debug, "RedisMessageProducer: Published message with topic {Topic} and id {Id} and body: {Request} to queues: {Queues}")]
            public static partial void PublishedMessage(ILogger logger, string topic, string id, string request, string queues);
        }
    }
}

