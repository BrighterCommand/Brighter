#region Licence
/* The MIT License (MIT)
Copyright © 2014 Toby Henderson

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

namespace Paramore.Brighter.MessagingGateway.RMQ.Async
{
    public class RmqMessageConsumerFactory : IAmAMessageConsumerFactory
    {
        private readonly RmqMessagingGatewayConnection _rmqConnection;
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
        /// Initializes a new instance of the <see cref="RmqMessageConsumerFactory"/> class.
        /// </summary>
        /// <param name="rmqConnection">The subscription to the broker hosting the queue</param>
        /// <param name="scheduler">Optional scheduler for delayed requeue operations</param>
        public RmqMessageConsumerFactory(RmqMessagingGatewayConnection rmqConnection, IAmAMessageScheduler? scheduler = null)
        {
            _rmqConnection = rmqConnection;
            _scheduler = scheduler;
        }

        /// <summary>
        /// Creates a consumer for the specified queue.
        /// </summary>
        /// <remarks>
        ///  Consider using Paramore.Brighter.MessagingGateway.Sync instead of using this method. That assembly continues to RMQ.Client V6.X.X
        /// which is designed for synchronous operation. This assembly uses RMQ.Client V7.X.X which is designed for asynchronous operation.
        /// As a result, this version uses the BrighterSynchronizationContext to block on async calls. Usage of the V6 library may be more reliable in production
        /// where you want to use a synchronous consumer.
        /// </remarks>
        /// <param name="subscription">The queue to connect to</param>
        /// <returns>IAmAMessageConsumerSync.</returns>
        public IAmAMessageConsumerSync Create(Subscription subscription)
        {
            RmqSubscription? rmqSubscription = subscription as RmqSubscription;
            if (rmqSubscription == null)
                throw new ConfigurationException("We expect an SQSConnection or SQSConnection<T> as a parameter");

            return new RmqMessageConsumer(
                _rmqConnection,
                rmqSubscription.ChannelName, //RMQ Queue Name
                rmqSubscription.RoutingKey,
                rmqSubscription.IsDurable,
                rmqSubscription.HighAvailability,
                rmqSubscription.BufferSize,
                rmqSubscription.DeadLetterChannelName,
                rmqSubscription.DeadLetterRoutingKey,
                rmqSubscription.Ttl,
                rmqSubscription.MaxQueueLength,
                subscription.MakeChannels,
                rmqSubscription.QueueType,
                scheduler: _scheduler);
        }

        public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
        {
            RmqSubscription? rmqSubscription = subscription as RmqSubscription;
            if (rmqSubscription == null)
                throw new ConfigurationException("We expect an SQSConnection or SQSConnection<T> as a parameter");

            return new RmqMessageConsumer(
                _rmqConnection,
                rmqSubscription.ChannelName, //RMQ Queue Name
                rmqSubscription.RoutingKey,
                rmqSubscription.IsDurable,
                rmqSubscription.HighAvailability,
                rmqSubscription.BufferSize,
                rmqSubscription.DeadLetterChannelName,
                rmqSubscription.DeadLetterRoutingKey,
                rmqSubscription.Ttl,
                rmqSubscription.MaxQueueLength,
                subscription.MakeChannels,
                rmqSubscription.QueueType,
                scheduler: _scheduler);
        }
    }
}
