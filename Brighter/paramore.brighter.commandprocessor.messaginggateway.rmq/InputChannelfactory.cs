// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messaginggateway.rmq
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-29-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

#region Licence
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

namespace paramore.brighter.commandprocessor.messaginggateway.rmq
{
    /// <summary>
    /// Class RMQInputChannelFactory.
    /// Creates instances of <see cref="IAmAChannel"/>channels. Supports the creation of AMQP Application Layer channels using RabbitMQ
    /// </summary>
    public class InputChannelFactory : IAmAChannelFactory
    {
        private readonly RmqMessageConsumerFactory _messageConsumerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="InputChannelFactory"/> class.
        /// </summary>
        /// <param name="messageConsumerFactory">The messageConsumerFactory.</param>
        public InputChannelFactory(RmqMessageConsumerFactory messageConsumerFactory)
        {
            _messageConsumerFactory = messageConsumerFactory;
        }

        /// <summary>
        /// Creates the input channel.
        /// </summary>
        /// <param name="channelName">Name of the channel.</param>
        /// <returns>IAmAnInputChannel.</returns>
        public IAmAnInputChannel CreateInputChannel(string channelName, string routingKey)
        {
            return new InputChannel(channelName, routingKey, _messageConsumerFactory.Create(channelName, routingKey));
        }

        /// <summary>
        /// Creates the output channel.
        /// </summary>
        /// <param name="channelName">Name of the channel.</param>
        /// <returns>IAmAnInputChannel.</returns>
        public IAmAnInputChannel CreateOutputChannel(string channelName, string routingKey)
        {
            return new InputChannel(channelName, routingKey, _messageConsumerFactory.Create(channelName, routingKey));
        }
    }
}
