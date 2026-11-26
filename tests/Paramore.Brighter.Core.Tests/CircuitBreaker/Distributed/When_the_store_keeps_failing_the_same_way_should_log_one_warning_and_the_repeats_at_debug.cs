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

using System;

using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class RepeatedStoreFailureLoggingTests
{
    [Fact]
    public async Task When_the_store_keeps_failing_the_same_way_should_log_one_warning_and_the_repeats_at_debug()
    {
        // Arrange
        using var context = TestCorrelator.CreateContext();
        var breaker = new DistributedOutboxCircuitBreaker(new ThrowingOnReadTrippedTopicStore(), new OutboxCircuitBreakerOptions());

        // Act
        await breaker.ReadTrippedTopicsAsync();
        await breaker.ReadTrippedTopicsAsync();
        await breaker.ReadTrippedTopicsAsync();

        // Assert
        var events = TestCorrelator.GetLogEventsFromCurrentContext().ToList();
        Assert.Single(events, e => e.Level == LogEventLevel.Warning && e.RenderMessage().Contains("InvalidOperationException"));
        Assert.Equal(2, events.Count(e => e.Level == LogEventLevel.Debug && e.RenderMessage().Contains("InvalidOperationException")));
    }
}
