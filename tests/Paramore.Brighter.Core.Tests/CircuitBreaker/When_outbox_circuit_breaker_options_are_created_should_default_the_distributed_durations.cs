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
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker;

public class OutboxCircuitBreakerOptionsDistributedDefaultsTests
{
    [Fact]
    public void When_outbox_circuit_breaker_options_are_created_should_default_the_distributed_durations()
    {
        // Arrange & Act
        var options = new OutboxCircuitBreakerOptions();

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(50), options.CooldownDuration);
        Assert.Equal(TimeSpan.Zero, options.ClockSkewLeeway);
        Assert.Equal(TimeSpan.FromSeconds(2), options.StoreTimeout);

        Assert.Equal(10, options.CooldownCount);
        Assert.Equal(TimeSpan.FromMinutes(5), options.MaximumTripDuration);
    }
}
