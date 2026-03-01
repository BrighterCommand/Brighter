#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// Factory class for creating in-memory channels.
    /// </summary>
    public class InMemoryChannelFactory : IAmAChannelFactory, IAmAChannelFactoryWithScheduler
    {
        private readonly InternalBus _internalBus;
        private readonly TimeProvider _timeProvider;
        private readonly TimeSpan? _ackTimeout;
        /// <summary>
        /// Gets or sets the message scheduler for delayed requeue support.
        /// </summary>
        public IAmAMessageScheduler? Scheduler { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InMemoryChannelFactory"/> class.
        /// </summary>
        /// <param name="internalBus">The internal bus for message routing.</param>
        /// <param name="timeProvider">The time provider for managing time-related operations.</param>
        /// <param name="ackTimeout">Optional acknowledgment timeout.</param>
        /// <param name="scheduler">Optional scheduler for delayed requeue operations.</param>
        public InMemoryChannelFactory(InternalBus internalBus, TimeProvider timeProvider, TimeSpan? ackTimeout = null, IAmAMessageScheduler? scheduler = null)
        {
            _internalBus = internalBus;
            _timeProvider = timeProvider;
            _ackTimeout = ackTimeout;
            Scheduler = scheduler;
        }

        /// <summary>
        /// Creates a synchronous channel.
        /// </summary>
        /// <param name="subscription">The subscription details for the channel.</param>
        /// <returns>A synchronous channel instance.</returns>
        public IAmAChannelSync CreateSyncChannel(Subscription subscription)
        {
            var deadLetterSupport = subscription as IUseBrighterDeadLetterSupport;
            var deadLetterKey = deadLetterSupport?.DeadLetterRoutingKey; 
            
            var invalidMessageSupport = subscription as IUseBrighterInvalidMessageSupport;
            var invalidMessageKey = invalidMessageSupport?.InvalidMessageRoutingKey;
            
            return new Channel(
                subscription.ChannelName,
                subscription.RoutingKey,
                new InMemoryMessageConsumer(
                    subscription.RoutingKey, 
                    _internalBus, 
                    _timeProvider,
                    deadLetterKey,  
                    invalidMessageKey,
                    ackTimeout: _ackTimeout,
                    scheduler: Scheduler),
                subscription.BufferSize
            );
        }

        /// <summary>
        /// Creates an asynchronous channel.
        /// </summary>
        /// <param name="subscription">The subscription details for the channel.</param>
        /// <returns>An asynchronous channel instance.</returns>
        public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
        {
            var deadLetterSupport = subscription as IUseBrighterDeadLetterSupport;
            var deadLetterKey = deadLetterSupport?.DeadLetterRoutingKey;

            var invalidMessageSupport = subscription as IUseBrighterInvalidMessageSupport;
            var invalidMessageKey = invalidMessageSupport?.InvalidMessageRoutingKey;

            return new ChannelAsync(
                subscription.ChannelName,
                subscription.RoutingKey,
                new InMemoryMessageConsumer(
                    subscription.RoutingKey,
                    _internalBus,
                    _timeProvider,
                    deadLetterKey,
                    invalidMessageKey,
                    ackTimeout: _ackTimeout,
                    scheduler: Scheduler),
                subscription.BufferSize
            );
        }

        /// <summary>
        /// Asynchronously creates an asynchronous channel.
        /// </summary>
        /// <param name="subscription">The subscription details for the channel.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, with an asynchronous channel instance as the result.</returns>
        public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription, CancellationToken cancellationToken = default)
        {
            var deadLetterSupport = subscription as IUseBrighterDeadLetterSupport;
            var deadLetterKey = deadLetterSupport?.DeadLetterRoutingKey;

            var invalidMessageSupport = subscription as IUseBrighterInvalidMessageSupport;
            var invalidMessageKey = invalidMessageSupport?.InvalidMessageRoutingKey;

            IAmAChannelAsync channel = new ChannelAsync(
                subscription.ChannelName,
                subscription.RoutingKey,
                new InMemoryMessageConsumer(
                    subscription.RoutingKey,
                    _internalBus,
                    _timeProvider,
                    deadLetterKey,
                    invalidMessageKey,
                    ackTimeout: _ackTimeout,
                    scheduler: Scheduler),
                subscription.BufferSize
            );
            return Task.FromResult(channel);
        }
    }
}
