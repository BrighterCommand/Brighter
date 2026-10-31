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
