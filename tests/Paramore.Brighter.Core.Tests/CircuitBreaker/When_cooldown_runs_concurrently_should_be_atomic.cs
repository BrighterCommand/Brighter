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
using System.Threading.Tasks;
using Paramore.Brighter;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker
{
    public class CircuitBreakerCoolDownConcurrencyTests
    {
        [Fact]
        public async Task When_cooldown_races_concurrent_trips_should_remain_consistent()
        {
            // Arrange: many tripped topics with CooldownCount 0, so a single CoolDown evicts each topic.
            // That maximises the read-modify-write / read-after-remove window CoolDown must survive.
            var breaker = new InMemoryOutboxCircuitBreaker(new OutboxCircuitBreakerOptions { CooldownCount = 0 });
            var topics = Enumerable.Range(0, 50).Select(i => new RoutingKey($"topic-{i}")).ToArray();
            foreach (var topic in topics)
                breaker.TripTopic(topic);

            // Act: cool down from several threads while other threads keep re-tripping the same topics.
            // A non-atomic decrement (or a second indexer read of an already-removed key) corrupts state
            // or throws KeyNotFoundException under this contention.
            var cooldowns = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < 200; i++)
                    breaker.CoolDown();
            }));
            var trips = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < 200; i++)
                    foreach (var topic in topics)
                        breaker.TripTopic(topic);
            }));

            var race = Record.ExceptionAsync(() => Task.WhenAll(cooldowns.Concat(trips)));

            // Assert: the concurrent cool-downs never fault.
            Assert.Null(await race);

            // Assert: state is not corrupted — once tripping stops, cooling down drains every topic.
            breaker.CoolDown();
            Assert.Empty(breaker.TrippedTopics);
        }
    }
}
