#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.Confirmation.TestDoubles
{
    /// <summary>
    /// A circuit-breaker test double whose <see cref="TripTopic"/> throws, modelling a fault in the
    /// confirmation callback's dispatch/trip work (as opposed to the observability span). Used to exercise
    /// the callback's local error-isolation so a misbehaving breaker cannot destabilise the producer thread.
    /// </summary>
    internal sealed class ThrowingOutboxCircuitBreaker : IAmAnOutboxCircuitBreaker
    {
        public void CoolDown() { }

        public void TripTopic(RoutingKey topic)
            => throw new InvalidOperationException("Circuit breaker failure injected by the test");

        public IEnumerable<RoutingKey> TrippedTopics => Enumerable.Empty<RoutingKey>();
    }
}
