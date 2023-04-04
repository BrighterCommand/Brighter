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

    public class RedisMessageProducer : RedisMessageGateway, IAmAMessageProducerSync
    {
        /// <summary>
        /// How many outstanding messages may the outbox have before we terminate the programme with an OutboxLimitReached exception?
        /// -1 => No limit, although the Outbox may discard older entries which is implementation dependent
        /// 0 => No outstanding messages, i.e. throw an error as soon as something goes into the Outbox
        /// 1+ => Allow this number of messages to stack up in an Outbox before throwing an exception (likely to fail fast)
        /// </summary>
        public int MaxOutStandingMessages { get; set;  } = -1;
        /// <summary>
        /// At what interval should we check the number of outstanding messages has not exceeded the limit set in MaxOutStandingMessages
        /// We spin off a thread to check when inserting an item into the outbox, if the interval since the last insertion is greater than this threshold
        /// If you set MaxOutStandingMessages to -1 or 0 this property is effectively ignored
        /// </summary>
        public int MaxOutStandingCheckIntervalMilliSeconds { get; set; } = 0;

        /// <summary>
        /// An outbox may require additional arguments before it can run its checks. The DynamoDb outbox for example expects there to be a Topic in the args
        /// This bag provides the args required
        /// </summary>
        public Dictionary<string, object> OutBoxBag { get; set; } = new Dictionary<string, object>();

        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RedisMessageProducer>();
        private readonly Publication _publication; 
        private const string NEXT_ID = "nextid";
        private const string QUEUES = "queues";

         public RedisMessageProducer(
             RedisMessagingGatewayConfiguration redisMessagingGatewayConfiguration, 
             RedisMessagePublication publication)
         
            : base(redisMessagingGatewayConfiguration)
         {
             _publication = publication;
             MaxOutStandingMessages = _publication.MaxOutStandingMessages;
             MaxOutStandingCheckIntervalMilliSeconds = _publication.MaxOutStandingCheckIntervalMilliSeconds;
             OutBoxBag = publication.OutBoxBag;
         }

        public void Dispose()
        {
            DisposePool();
            GC.SuppressFinalize(this);
        }

       /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Task.</returns>
        public void Send(Message message)
        {
            using (var client = Pool.Value.GetClient())
            {
                Topic = message.Header.Topic;

                s_logger.LogDebug("RedisMessageProducer: Preparing to send message");
  
                var redisMessage = CreateRedisMessage(message);

                s_logger.LogDebug("RedisMessageProducer: Publishing message with topic {Topic} and id {Id} and body: {Request}", 
                    message.Header.Topic, message.Id.ToString(), message.Body.Value);
                //increment a counter to get the next message id
                var nextMsgId = IncrementMessageCounter(client);
                //store the message, against that id
                StoreMessage(client, redisMessage, nextMsgId);
                //If there are subscriber queues, push the message to the subscriber queues
                var pushedTo = PushToQueues(client, nextMsgId);
                s_logger.LogDebug("RedisMessageProducer: Published message with topic {Topic} and id {Id} and body: {Request} to queues: {3}", 
                    message.Header.Topic, message.Id.ToString(), message.Body.Value, string.Join(", ", pushedTo));
            }
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delayMilliseconds">The sending delay</param>
        /// <returns>Task.</returns>
         public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            //No delay support implemented
            Send(message);
        }
 

        private IEnumerable<string> PushToQueues(IRedisClient client, long nextMsgId)
        {
            var key = Topic + "." + QUEUES;
            var queues = client.GetAllItemsFromSet(key).ToList();
            foreach (var queue in queues)
            {
                //First add to the queue itself
                client.AddItemToList(queue, nextMsgId.ToString());
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

   }
}
