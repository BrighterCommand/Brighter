#region Licence
/* The MIT License (MIT)
Copyright © 2017 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

namespace Paramore.Brighter.MessagingGateway.MQTT
{
    /// <summary>
    /// Creates <see cref="MqttMessageConsumer"/> instances for MQTT subscriptions.
    /// </summary>
    public class MqttMessageConsumerFactory : IAmAMessageConsumerFactory
    {
        private readonly MqttMessagingGatewayConsumerConfiguration _configuration;
        private IAmAMessageScheduler? _scheduler;

        /// <summary>
        /// Gets or sets the message scheduler for delayed requeue support.
        /// Can be set after construction to allow channel factories to forward the scheduler from DI.
        /// </summary>
        public IAmAMessageScheduler? Scheduler
        {
            get => _scheduler;
            set => _scheduler = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MqttMessageConsumerFactory"/> class.
        /// </summary>
        /// <param name="configuration">The MQTT messaging gateway consumer configuration</param>
        /// <param name="scheduler">The optional message scheduler for delayed requeue support</param>
        public MqttMessageConsumerFactory(
            MqttMessagingGatewayConsumerConfiguration configuration,
            IAmAMessageScheduler? scheduler = null)
        {
            _configuration = configuration;
            _scheduler = scheduler;
        }

        /// <summary>
        /// Creates a synchronous consumer for the specified subscription.
        /// </summary>
        /// <param name="subscription">The subscription to create a consumer for</param>
        /// <returns>IAmAMessageConsumerSync</returns>
        public IAmAMessageConsumerSync Create(Subscription subscription)
        {
            return new MqttMessageConsumer(_configuration, _scheduler);
        }

        /// <summary>
        /// Creates an asynchronous consumer for the specified subscription.
        /// </summary>
        /// <param name="subscription">The subscription to create a consumer for</param>
        /// <returns>IAmAMessageConsumerAsync</returns>
        public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
        {
            return new MqttMessageConsumer(_configuration, _scheduler);
        }
    }
}
