#region License

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


using System.Collections.Generic;

namespace Paramore.Brighter.CircuitBreaker
{
    public interface IAmAnOutboxCircuitBreaker
    {
        /// <summary>
        /// Each time an attempt to ClearOutstandingFromOutbox the IAmAnOutboxCircuitBreaker is cooled down,
        /// this decrements the tripped topics, making them available for publication once the Cooldown
        /// period is set to zero.
        /// </summary>
        public void CoolDown();

        /// <summary>
        /// If a topic exceeds a configurable failure threshold within a time window, mark it as "tripped" 
        /// </summary>
        /// <param name="topic">Name of the entity to circuit break</param>
        public void TripTopic(RoutingKey topic);

        /// <summary>
        /// A collection of tripped topics.
        /// </summary>
        public IEnumerable<RoutingKey> TrippedTopics { get; }
    }
}
