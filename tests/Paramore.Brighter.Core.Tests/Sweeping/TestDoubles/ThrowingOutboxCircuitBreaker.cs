using System;
using System.Collections.Generic;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// An <see cref="IAmAnOutboxCircuitBreaker"/> whose <see cref="TrippedTopics"/> throws an
/// <see cref="InvalidOperationException"/> with the caller's message; tripping and cooling down do nothing.
/// </summary>
public sealed class ThrowingOutboxCircuitBreaker(string message) : IAmAnOutboxCircuitBreaker
{
    public void CoolDown() { }

    public void TripTopic(RoutingKey topic) { }

    public IEnumerable<RoutingKey> TrippedTopics => throw new InvalidOperationException(message);
}
