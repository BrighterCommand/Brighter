using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class DistributedBreakerOneStoreTimeoutTests
{
    private static readonly TimeSpan s_storeTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan s_eachCall = TimeSpan.FromSeconds(1.5);

    [Fact]
    public async Task When_the_two_read_calls_together_take_longer_than_one_store_timeout_should_abandon_the_read_at_the_timeout()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var store = new ClockAdvancingReadsTrippedTopicStore(new InMemoryTrippedTopicStore(), clock, s_eachCall);
        var breaker = new DistributedOutboxCircuitBreaker(store, new OutboxCircuitBreakerOptions { StoreTimeout = s_storeTimeout }, timeProvider: clock);

        // Act
        await breaker.ReadTrippedTopicsAsync();

        // Assert
        Assert.True(breaker.StoreStatus.IsUsingFallback);
        Assert.Equal("timeout", breaker.StoreStatus.LastFailure);
    }
}
