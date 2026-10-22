#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Collections.Generic;

namespace Paramore.Brighter.ServiceActivator
{
    /// <summary>
    /// A consistent copy of a <see cref="Dispatcher"/>'s lifecycle, taken under its lifecycle lock.
    /// </summary>
    public readonly record struct DispatcherLifecycleSnapshot
    {
        /// <summary>
        /// Creates a snapshot.
        /// </summary>
        /// <param name="state">The dispatcher's state.</param>
        /// <param name="endRequested">Has <see cref="Dispatcher.End"/> been called since the dispatcher last started?</param>
        /// <param name="shutSubscriptions">The subscriptions shut on purpose.</param>
        /// <param name="consumerOperationsInFlight">The number of consumer operations begun and not finished.</param>
        /// <param name="expectedConsumers">The expected consumer count for each subscription name.</param>
        /// <param name="consumers">A copy of the dispatcher's consumers.</param>
        public DispatcherLifecycleSnapshot(
            DispatcherState state,
            bool endRequested,
            IReadOnlyCollection<SubscriptionName> shutSubscriptions,
            int consumerOperationsInFlight,
            IReadOnlyDictionary<SubscriptionName, int> expectedConsumers,
            IReadOnlyList<IAmAConsumer> consumers)
        {
            State = state;
            EndRequested = endRequested;
            ShutSubscriptions = shutSubscriptions;
            ConsumerOperationsInFlight = consumerOperationsInFlight;
            ExpectedConsumers = expectedConsumers;
            Consumers = consumers;
        }

        /// <summary>The dispatcher's state at the snapshot.</summary>
        public DispatcherState State { get; }

        /// <summary>True from any call to <see cref="Dispatcher.End"/> until the dispatcher next starts.</summary>
        public bool EndRequested { get; }

        /// <summary>The subscriptions shut on purpose.</summary>
        public IReadOnlyCollection<SubscriptionName> ShutSubscriptions { get; }

        /// <summary>The number of consumer operations that have begun and not finished.</summary>
        public int ConsumerOperationsInFlight { get; }

        /// <summary>Each subscription's expected consumer count; counts for subscriptions with the same name are summed.</summary>
        public IReadOnlyDictionary<SubscriptionName, int> ExpectedConsumers { get; }

        /// <summary>A copy of the dispatcher's consumers.</summary>
        public IReadOnlyList<IAmAConsumer> Consumers { get; }
    }
}
