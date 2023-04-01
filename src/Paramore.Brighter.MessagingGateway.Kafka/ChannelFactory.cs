#region Licence
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
    /// <summary>
    /// Abstracts a Kafka channel. A channel is a logically addressable pipe.
    /// </summary>
    public class ChannelFactory : IAmAChannelFactory
    {
        private readonly KafkaMessageConsumerFactory _kafkaMessageConsumerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChannelFactory"/> class.
        /// </summary>
        /// <param name="kafkaMessageConsumerFactory">The messageConsumerFactory.</param>
        public ChannelFactory(KafkaMessageConsumerFactory kafkaMessageConsumerFactory)
        {
            _kafkaMessageConsumerFactory = kafkaMessageConsumerFactory;
        }

        /// <summary>
        /// Creates the input channel
        /// </summary>
        /// <param name="subscription">The subscription parameters with which to create the channel</param>
        /// <returns></returns>
        public IAmAChannel CreateChannel(Subscription subscription)
        {
            KafkaSubscription rmqSubscription = subscription as KafkaSubscription;
            if (rmqSubscription == null)
                throw new ConfigurationException("We expect an KafkaSubscription or KafkaSubscription<T> as a parameter");

            return new Channel(
                subscription.ChannelName,
                _kafkaMessageConsumerFactory.Create(subscription),
                subscription.BufferSize);
        }
    }
}
