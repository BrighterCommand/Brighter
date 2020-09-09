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
using System.Threading.Tasks;
using Paramore.Brighter.Logging;
using StackExchange.Redis;

namespace Paramore.Brighter.MessagingGateway.RedisStreams
{
    public class RedisStreamsProducer : RedisStreamsGateway, IAmAMessageProducer
    { 
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<RedisStreamsProducer>);
        private const string NEXT_ID = "nextid";
        private const string QUEUES = "queues";

        public RedisStreamsProducer(RedisStreamsConfiguration redisStreamsConfiguration)
            : base(redisStreamsConfiguration)
        {}

       public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~RedisStreamsProducer()
        {
            ReleaseUnmanagedResources();
        }

        /// <summary>
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Task.</returns>
        public void Send(Message message)
        {
            _logger.Value.DebugFormat("RedisMessageProducer: Preparing to send message");

            try
            {
                var redisMessage = RedisStreamsPublisher.Create(message);

                _logger.Value.DebugFormat("RedisMessageProducer: Publishing message with topic {0} and id {1} and body: {2}", 
                        message.Header.Topic, message.Id.ToString(), message.Body.Value);

                var db = Redis.GetDatabase();
                var messageId = db.StreamAdd(message.Header.Topic, redisMessage);
                
               _logger.Value.DebugFormat("RedisMessageProducer: Published message with topic {0} and id {1} and body: {2}", 
                        message.Header.Topic, message.Id.ToString(), message.Body.Value);
     
            }
            catch (Exception e)
            {
                _logger.Value.ErrorFormat(
                    "RedisStreamsProducer: Error talking to the Redis on {0}, resetting connection",
                    Redis.Configuration 
                    );
 
                throw new ChannelFailureException("Error talking to the broker, see inner exception for details", e);
            }
        }
        
        /// Sends the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="delayMilliseconds">The sending delay</param>
        /// <returns>Task.</returns>
        public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            //No delay support implemented
            Task.Delay(delayMilliseconds).Wait();
            Send(message);
}

        private void ReleaseUnmanagedResources()
        {
            CloseRedisClient();
        }

         

  }
}
