#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Threading.Tasks;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Interface for a consumer that processes messages from a subscription.
    /// </summary>
    /// <remarks>
    /// Consumers represent individual worker instances that process messages from channels.
    /// They are managed by the dispatcher and can be started, stopped, and monitored.
    /// </remarks>
    public interface IAmAConsumer : IDisposable
    {
        /// <summary>
        /// Gets the name of this consumer.
        /// </summary>
        /// <value>The <see cref="ConsumerName"/> identifying this consumer.</value>
        ConsumerName Name { get; }
        
        /// <summary>
        /// Gets or sets the subscription that this Consumer is processing.
        /// </summary>
        /// <value>The <see cref="Subscription"/> being processed by this consumer.</value>
        Subscription Subscription { get; set; }

        /// <summary>
        /// Gets the performer that executes the message processing logic.
        /// </summary>
        /// <value>The <see cref="IAmAPerformer"/> responsible for message processing.</value>
        IAmAPerformer Performer { get; }

        /// <summary>
        /// Gets or sets the current state of the consumer.
        /// </summary>
        /// <value>The <see cref="ConsumerState"/> indicating the current operational state.</value>
        ConsumerState State { get; set; }

        /// <summary>
        /// Gets or sets the background task running the consumer.
        /// </summary>
        /// <value>The <see cref="Task"/> representing the consumer's background work, or null if not running.</value>
        Task? Job { get; set; }

        /// <summary>
        /// Gets or sets the identifier for the background job.
        /// </summary>
        /// <value>The <see cref="int"/> job identifier.</value>
        int JobId { get; set; }

        /// <summary>
        /// Opens the task queue and begins receiving messages.
        /// </summary>
        /// <remarks>
        /// This starts the consumer's background processing and begins consuming messages from the subscription.
        /// </remarks>
        void Open();

        /// <summary>
        /// Shuts down the task, which will stop receiving messages.
        /// </summary>
        /// <param name="subscriptionRoutingKey">The <see cref="RoutingKey"/> of the subscription to shut down.</param>
        void Shut(RoutingKey subscriptionRoutingKey);
    }
}
