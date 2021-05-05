﻿#region Licence

/* The MIT License (MIT)
Copyright © 2019 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections.Concurrent;
using System.Threading.Tasks;
 using Microsoft.Extensions.Logging;
 using Paramore.Brighter.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Paramore.Brighter.MessagingGateway.RMQ
{
    public class PullConsumer : DefaultBasicConsumer
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<RmqMessageConsumer>();
        
        //we do end up creating a second buffer to the Brighter Channel, but controlling the flow from RMQ depends
        //on us being able to buffer up to the set QoS and then pull. This matches other implementations.
        private readonly ConcurrentQueue<BasicDeliverEventArgs> _messages = new ConcurrentQueue<BasicDeliverEventArgs>();

        public PullConsumer(IModel channel, ushort batchSize)
            : base(channel)
        {
            //set the number of messages to fetch -- defaults to 1 unless set on subscription, no impact on
            //BasicGet, only works on BasicConsume
            channel.BasicQos(0, batchSize, false);
        }

        /// <summary>
        /// Used to pull from the buffer of messages delivered to us via BasicConsumer
        /// </summary>
        /// <param name="timeoutInMilliseconds">The total time to spend waiting for the buffer to fill up to bufferSize</param>
        /// <param name="bufferSize">The size of the buffer we want to fill wit messages</param>
        /// <returns>A tuple containing: the number of messages in the buffer, and the buffer itself</returns>
        public (int, BasicDeliverEventArgs[]) DeQueue(int timeoutInMilliseconds, int bufferSize)
        {
            var now = DateTime.UtcNow;
            var end = now.AddMilliseconds(timeoutInMilliseconds);
            var pause = ((timeoutInMilliseconds > 25) && (timeoutInMilliseconds / 5 > 5)) ? timeoutInMilliseconds / 5 : 5;
 
            
            var buffer = new BasicDeliverEventArgs[bufferSize];
            var bufferIndex = 0;
            
            
            while (now < end && bufferIndex < bufferSize)
            {
                if (_messages.TryDequeue(out BasicDeliverEventArgs result))
                {
                    buffer[bufferIndex] = result;
                    ++bufferIndex;
                }
                else
                {
                    Task.Delay(pause).Wait();
                }
                now = DateTime.UtcNow;
            }

            return bufferIndex == 0 ? (0, null) : (bufferIndex, buffer);
        }
        
         public override void HandleBasicDeliver(
            string consumerTag, 
            ulong deliveryTag, 
            bool redelivered, 
            string exchange, 
            string routingKey,
            IBasicProperties properties, 
            ReadOnlyMemory<byte> body)
        {
            //We have to copy the body, before returning, as the memory in body is pooled and may be re-used after (see base class documentation)
            //See also https://docs.microsoft.com/en-us/dotnet/standard/memory-and-spans/memory-t-usage-guidelines
            var payload = new byte[body.Length];
            body.CopyTo(payload);
            
            _messages.Enqueue(new BasicDeliverEventArgs
            {
                BasicProperties = properties,
                Body = payload,
                ConsumerTag = consumerTag,
                DeliveryTag = deliveryTag,
                Exchange = exchange,
                Redelivered = redelivered,
                RoutingKey = routingKey
            });
        }

        public override void OnCancel(params string[] consumerTags)
        {
            //try  to nack anything in the buffer.
            try
            {
                foreach (var message in _messages)
                {
                    Model.BasicNack(message.DeliveryTag, false, true);
                }
            }
            catch (Exception e)
            {
                //don't impede shutdown, just log
                s_logger.LogWarning("Tried to nack unhandled messages on shutdown but failed for {ErrorMessage}",
                    e.Message);
            }
           
            base.OnCancel();
        }

   }
}
