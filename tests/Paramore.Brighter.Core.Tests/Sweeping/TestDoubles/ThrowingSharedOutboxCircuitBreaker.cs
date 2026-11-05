using System;
using System.Collections.Generic;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// A shared circuit breaker whose <see cref="TrippedTopics"/> throws an <see cref="InvalidOperationException"/>
/// and whose <see cref="StoreStatus"/> is the status it was given; tripping, cooling down and refreshing do nothing.
/// </summary>
public sealed class ThrowingSharedOutboxCircuitBreaker(TrippedTopicStoreStatus storeStatus) : IAmAnOutboxCircuitBreaker, IAmASharedOutboxCircuitBreaker
{
    public void CoolDown() { }

    public void TripTopic(RoutingKey topic) { }

    public IEnumerable<RoutingKey> TrippedTopics => throw new InvalidOperationException("tripped topics unreadable");

    public void StartRefresh() { }

    public TrippedTopicStoreStatus StoreStatus => storeStatus;
}
