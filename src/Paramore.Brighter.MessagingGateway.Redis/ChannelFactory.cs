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

using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.Redis
{
    /// <summary>
    /// Creates instances of <see cref="IAmAChannelSync"/> channels for the Redis messaging gateway.
    /// </summary>
    public class ChannelFactory : IAmAChannelFactory, IAmAChannelFactoryWithScheduler
    {
        private readonly RedisMessageConsumerFactory _messageConsumerFactory;

        /// <summary>
        /// Gets or sets the message scheduler for delayed requeue support.
        /// Setting this property forwards the scheduler to the underlying consumer factory.
        /// </summary>
        public IAmAMessageScheduler? Scheduler
        {
            get => _messageConsumerFactory.Scheduler;
            set => _messageConsumerFactory.Scheduler = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChannelFactory"/> class.
        /// </summary>
        /// <param name="messageConsumerFactory">The messageConsumerFactory.</param>
        public ChannelFactory(RedisMessageConsumerFactory messageConsumerFactory)
        {
            _messageConsumerFactory = messageConsumerFactory;
        }

        /// <summary>
        /// Creates the input channel.
        /// </summary>
        /// <param name="subscription">The subscription parameters with which to create the channel</param>
        /// <returns>An <see cref="IAmAChannel"/> that provides access to a stream or queue</returns>
        public IAmAChannelSync CreateSyncChannel(Subscription subscription)
        {
            RedisSubscription? rmqSubscription = subscription as RedisSubscription;  
            if (rmqSubscription == null)
                throw new ConfigurationException("We expect an RedisSubscription or RedisSubscription<T> as a parameter");
            
            return new Channel(
                subscription.ChannelName, 
                subscription.RoutingKey, 
                _messageConsumerFactory.Create(subscription),
                subscription.BufferSize
                );
        }

        /// <summary>
        /// Creates the input channel.
        /// </summary>
        /// <param name="subscription">The subscription parameters with which to create the channel</param>
        /// <returns>An <see cref="IAmAChannelAsync"/> that provides access to a stream or queue</returns>
         public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
        {
            RedisSubscription? rmqSubscription = subscription as RedisSubscription;  
            if (rmqSubscription == null)
                throw new ConfigurationException("We expect an RedisSubscription or RedisSubscription<T> as a parameter");
            
            return new ChannelAsync(
                subscription.ChannelName, 
                subscription.RoutingKey, 
                _messageConsumerFactory.CreateAsync(subscription),
                subscription.BufferSize
                );
        }

        public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken ct = default)
        {
            RedisSubscription? rmqSubscription = subscription as RedisSubscription;  
            if (rmqSubscription == null)
                throw new ConfigurationException("We expect an RedisSubscription or RedisSubscription<T> as a parameter");
            
            IAmAChannelAsync channel =  new ChannelAsync(
                subscription.ChannelName, 
                subscription.RoutingKey, 
                _messageConsumerFactory.CreateAsync(subscription),
                subscription.BufferSize
            );
            
            return Task.FromResult(channel);
        }
    }
}
