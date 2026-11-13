using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class DistributedBreakerEndExpiredFailureTests
{
    private readonly RoutingKey _orders = new("orders");

    [Fact]
    public async Task When_ending_expired_trips_fails_should_not_read_live_trips_and_enter_fallback()
    {
        // Arrange
        var store = new ReadsFailingOnDemandTrippedTopicStore(new InMemoryTrippedTopicStore(), "The expired trips cannot be ended") { FailReads = true };
        var breaker = new DistributedOutboxCircuitBreaker(store, new OutboxCircuitBreakerOptions(), timeProvider: new FakeTimeProvider());
        breaker.TripTopic(_orders);

        // Act
        await breaker.ReadTrippedTopicsAsync();

        // Assert
        Assert.Equal(0, store.LiveReadCount);
        Assert.True(breaker.StoreStatus.IsUsingFallback);
        Assert.Equal("InvalidOperationException", breaker.StoreStatus.LastFailure);
    }
}
