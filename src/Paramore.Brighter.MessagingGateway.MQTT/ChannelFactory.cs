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

using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.MQTT
{
    /// <summary>
    /// Creates MQTT channels that wrap <see cref="MqttMessageConsumer"/> instances.
    /// </summary>
    public class ChannelFactory : IAmAChannelFactory, IAmAChannelFactoryWithScheduler
    {
        private readonly MqttMessageConsumerFactory _consumerFactory;

        /// <summary>
        /// Gets or sets the message scheduler for delayed requeue support.
        /// Setting this property forwards the scheduler to the underlying consumer factory.
        /// </summary>
        public IAmAMessageScheduler? Scheduler
        {
            get => _consumerFactory.Scheduler;
            set => _consumerFactory.Scheduler = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChannelFactory"/> class.
        /// </summary>
        /// <param name="consumerFactory">The MQTT message consumer factory</param>
        public ChannelFactory(MqttMessageConsumerFactory consumerFactory)
        {
            _consumerFactory = consumerFactory;
        }

        /// <summary>
        /// Creates a synchronous channel for the specified subscription.
        /// </summary>
        /// <param name="subscription">The subscription to create a channel for</param>
        /// <returns>A synchronous channel wrapping an MQTT consumer</returns>
        public IAmAChannelSync CreateSyncChannel(Subscription subscription)
        {
            return new Channel(
                subscription.ChannelName,
                subscription.RoutingKey,
                _consumerFactory.Create(subscription),
                subscription.BufferSize
            );
        }

        /// <summary>
        /// Creates an asynchronous channel for the specified subscription.
        /// </summary>
        /// <param name="subscription">The subscription to create a channel for</param>
        /// <returns>An asynchronous channel wrapping an MQTT consumer</returns>
        public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
        {
            return new ChannelAsync(
                subscription.ChannelName,
                subscription.RoutingKey,
                _consumerFactory.CreateAsync(subscription),
                subscription.BufferSize
            );
        }

        /// <summary>
        /// Creates an asynchronous channel for the specified subscription.
        /// </summary>
        /// <param name="subscription">The subscription to create a channel for</param>
        /// <param name="ct">The cancellation token</param>
        /// <returns>An asynchronous channel wrapping an MQTT consumer</returns>
        public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default)
        {
            IAmAChannelAsync channel = new ChannelAsync(
                subscription.ChannelName,
                subscription.RoutingKey,
                _consumerFactory.CreateAsync(subscription),
                subscription.BufferSize
            );

            return Task.FromResult(channel);
        }
    }
}
