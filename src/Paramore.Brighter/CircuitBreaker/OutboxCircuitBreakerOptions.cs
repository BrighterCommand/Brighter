﻿#region License

/* The MIT License (MIT)
Copyright © 2025 Michael Freeman <mike.c.freeman@gmail.com>

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

# endregion

using System;

namespace Paramore.Brighter.CircuitBreaker
{

    /// <summary>
    /// The configuration options for <see cref="IAmAnOutboxCircuitBreaker"/>
    /// </summary>
    /// <remarks>
    /// <see cref="CooldownCount"/> and <see cref="MaximumTripDuration"/> are read only by <see cref="InMemoryOutboxCircuitBreaker"/>.
    /// <see cref="CooldownDuration"/>, <see cref="ClockSkewLeeway"/> and <see cref="StoreTimeout"/> are read only by <see cref="DistributedOutboxCircuitBreaker"/>.
    /// A breaker ignores the options it does not read, and does not reject them.
    /// </remarks>
    public class OutboxCircuitBreakerOptions
    {
        /// <value>
        /// Circuit break unhealthy topics for a specific cooldown period in ticks (on clear outbox).
        /// Read only by <see cref="InMemoryOutboxCircuitBreaker"/>.
        /// </value>
        public int CooldownCount { get; set; } = 10;

        /// <value>
        /// The longest a topic stays tripped, whatever the cooldown count. Must be greater than zero and at
        /// most 24 hours. Defaults to 5 minutes.
        /// Read only by <see cref="InMemoryOutboxCircuitBreaker"/>.
        /// </value>
        public TimeSpan MaximumTripDuration { get; set; } = TimeSpan.FromMinutes(5);

        /// <value>
        /// How long a trip shared through a store lasts, before the clock skew leeway is added. Defaults to 50 seconds.
        /// Read only by <see cref="DistributedOutboxCircuitBreaker"/>.
        /// </value>
        public TimeSpan CooldownDuration { get; set; } = TimeSpan.FromSeconds(50);

        /// <value>
        /// The allowance for clock skew between instances, added to <see cref="CooldownDuration"/> to give the trip
        /// length of a shared trip. Defaults to zero.
        /// Read only by <see cref="DistributedOutboxCircuitBreaker"/>.
        /// </value>
        public TimeSpan ClockSkewLeeway { get; set; } = TimeSpan.Zero;

        /// <value>
        /// The longest a read or write of the trip store may take. Defaults to 2 seconds.
        /// Read only by <see cref="DistributedOutboxCircuitBreaker"/>.
        /// </value>
        public TimeSpan StoreTimeout { get; set; } = TimeSpan.FromSeconds(2);
    }
}
