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
    /// the stop and start of individual connections through <see cref="Open"/> and <see cref="Shut"/>
    /// </summary>
    public interface IDispatcher
    {
        /// <summary>
        /// Gets the <see cref="Consumer"/>s
        /// </summary>
        /// <value>The consumers.</value>
        IEnumerable<IAmAConsumer> Consumers { get; }

        /// <summary>
        /// Gets or sets the name for this dispatcher instance.
        /// Used when communicating with this instance via the Control Bus
        /// </summary>
        /// <value>The name of the host.</value>
        HostName HostName { get; set; }

        /// <summary>
        /// Ends this instance.
        /// </summary>
        /// <returns>Task.</returns>
        Task End();

        /// <summary>
        /// Opens the specified subscription.
        /// </summary>
        /// <param name="subscription">The subscription.</param>
        void Open(Subscription subscription);

        /// <summary>
        /// Opens the specified subscription name.
        /// </summary>
        /// <param name="subscriptionName"></param>
        void Open(SubscriptionName subscriptionName);

        /// <summary>
        /// Begins listening for messages on channels, and dispatching them to request handlers.
        /// </summary>
        void Receive();

        /// <summary>
        /// Shuts the specified subscription.
        /// </summary>
        /// <param name="subscription">The subscription.</param>
        void Shut(Subscription subscription);

        /// <summary>
        /// Shuts the specified subscription name.
        /// </summary>
        /// <param name="subscriptionName">Name of the subscription.</param>
        void Shut(SubscriptionName subscriptionName);

        /// <summary>
        /// Get the current running state of the dispatcher
        /// </summary>
        /// <returns>Array of all available subscriptions and how many are currency running</returns>
        DispatcherStateItem[] GetState();

        void SetActivePerformers(string connectionName, int numberOfPerformers);
    }
}
