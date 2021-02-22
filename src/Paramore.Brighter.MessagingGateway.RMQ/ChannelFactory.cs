﻿#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.MessagingGateway.RMQ
{
    /// <summary>
    /// Class RMQInputChannelFactory.
    /// Creates instances of <see cref="IAmAChannel"/>channels. Supports the creation of AMQP Application Layer channels using RabbitMQ
    /// </summary>
    public class ChannelFactory : IAmAChannelFactory
    {
        private readonly RmqMessageConsumerFactory _messageConsumerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChannelFactory"/> class.
        /// </summary>
        /// <param name="messageConsumerFactory">The messageConsumerFactory.</param>
        public ChannelFactory(RmqMessageConsumerFactory messageConsumerFactory)
        {
            _messageConsumerFactory = messageConsumerFactory;
        }

        /// <summary>
        /// Creates the input channel.
        /// </summary>
        /// <param name="subscription">An RmqSubscription with parameters to create the queue with</param>
        /// <returns>IAmAnInputChannel.</returns>
        public IAmAChannel CreateChannel(Subscription subscription)
        {
            RmqSubscription rmqSubscription = subscription as RmqSubscription;  
            if (rmqSubscription == null)
                throw new ConfigurationException("We expect an RmqSubscription or RmqSubscription<T> as a parameter");

            var messageConsumer = _messageConsumerFactory.Create(rmqSubscription);
            
            return new Channel(
                channelName:subscription.ChannelName, 
                messageConsumer:messageConsumer, 
                maxQueueLength:subscription.BufferSize
                );
        }
    }
}
