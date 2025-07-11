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

using System.Collections.Generic;
using System.Threading.Tasks;
using Paramore.Brighter.ServiceActivator.Status;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// Interface IDispatcher
    /// The 'core' Service Activator class, the Dispatcher controls and co-ordinates the creation of readers from channels, and dispatching the commands and
    /// events translated from those messages to handlers. It controls the lifetime of the application through <see cref="Receive"/> and <see cref="End"/> and allows
    /// the stop and start of individual connections through <see cref="Open"/> and <see cref="Shut"/>.
    /// </summary>
    /// <remarks>
    /// The Dispatcher implements the Service Activator pattern and coordinates multiple message pumps
    /// to process messages from different channels concurrently.
    /// </remarks>
    public interface IDispatcher
    {
        /// <summary>
        /// Gets the <see cref="Consumer"/>s managed by this dispatcher.
        /// </summary>
        /// <value>An <see cref="IEnumerable{T}"/> of <see cref="IAmAConsumer"/> instances.</value>
        IEnumerable<IAmAConsumer> Consumers { get; }

        /// <summary>
        /// Gets or sets the name for this dispatcher instance.
        /// Used when communicating with this instance via the Control Bus.
        /// </summary>
        /// <value>The <see cref="HostName"/> of the dispatcher instance.</value>
        HostName HostName { get; set; }

        /// <summary>
        /// Ends this dispatcher instance, stopping all message processing.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task End();

        /// <summary>
        /// Opens the specified subscription for message processing.
        /// </summary>
        /// <param name="subscription">The <see cref="Subscription"/> to open.</param>
        void Open(Subscription subscription);

        /// <summary>
        /// Opens the specified subscription by name for message processing.
        /// </summary>
        /// <param name="subscriptionName">The <see cref="SubscriptionName"/> of the subscription to open.</param>
        void Open(SubscriptionName subscriptionName);

        /// <summary>
        /// Begins listening for messages on channels, and dispatching them to request handlers.
        /// </summary>
        /// <remarks>
        /// This method will typically block and run continuously until <see cref="End"/> is called.
        /// </remarks>
        void Receive();

        /// <summary>
        /// Shuts down the specified subscription, stopping message processing for that subscription.
        /// </summary>
        /// <param name="subscription">The <see cref="Subscription"/> to shut down.</param>
        void Shut(Subscription subscription);

        /// <summary>
        /// Shuts down the specified subscription by name, stopping message processing for that subscription.
        /// </summary>
        /// <param name="subscriptionName">The <see cref="SubscriptionName"/> of the subscription to shut down.</param>
        void Shut(SubscriptionName subscriptionName);

        /// <summary>
        /// Gets the current running state of the dispatcher.
        /// </summary>
        /// <returns>An array of <see cref="DispatcherStateItem"/> showing all available subscriptions and how many are currently running.</returns>
        DispatcherStateItem[] GetState();

        /// <summary>
        /// Sets the number of active performers for a specific connection.
        /// </summary>
        /// <param name="connectionName">The <see cref="string"/> name of the connection.</param>
        /// <param name="numberOfPerformers">The <see cref="int"/> number of performers to set.</param>
        void SetActivePerformers(string connectionName, int numberOfPerformers);
    }
}
