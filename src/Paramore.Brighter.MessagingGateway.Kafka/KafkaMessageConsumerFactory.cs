﻿#region Licence
/* The MIT License (MIT)
Copyright © 2015 Wayne Hunsley <whunsley@gmail.com>

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

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    /// <inheritdoc />
    /// <summary>
    /// Class KafkaMessageConsumerFactory.
    /// </summary>
    public class KafkaMessageConsumerFactory : IAmAMessageConsumerFactory
    {
        private readonly KafkaMessagingGatewayConfiguration _globalConfiguration;
        private readonly KafkaConsumerConfiguration _consumerConfiguration;

        public KafkaMessageConsumerFactory(KafkaMessagingGatewayConfiguration globalConfiguration,
            KafkaConsumerConfiguration consumerConfiguration)
        {
            _globalConfiguration = globalConfiguration;
            _consumerConfiguration = consumerConfiguration;
        }

        /// <summary>
        /// Creates a consumer for the specified queue.
        /// </summary>
        /// <param name="subscription">The queue to connect to</param>
        /// <returns>IAmAMessageConsumer</returns>
         public IAmAMessageConsumer Create(Subscription subscription)
        {
            return new KafkaMessageConsumer(
                subscription.ChannelName, //groupId,
                subscription.RoutingKey, //topic
                _globalConfiguration, 
                _consumerConfiguration);
        }
    }
}
