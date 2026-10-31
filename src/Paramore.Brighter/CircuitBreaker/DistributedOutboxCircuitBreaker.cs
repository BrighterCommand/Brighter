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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.CircuitBreaker;

/// <summary>
/// An outbox circuit breaker that shares its trips between instances through an <see cref="IAmATrippedTopicStore"/>.
/// </summary>
public class DistributedOutboxCircuitBreaker : IAmAnOutboxCircuitBreaker, IAmAnOutboxCircuitBreakerAsync, IAmASharedOutboxCircuitBreaker
{
    private static readonly TimeSpan s_longestPermittedDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan s_shortestPermittedStoreTimeout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan s_longestPermittedStoreTimeout = TimeSpan.FromSeconds(60);

    private readonly IAmATrippedTopicStore _store;
    private readonly TimeSpan _cooldownDuration;
    private readonly TimeSpan _clockSkewLeeway;
    private readonly TimeSpan _storeTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedOutboxCircuitBreaker"/> class.
    /// </summary>
    /// <param name="store">The store that shares trips between instances</param>
    /// <param name="options">The cooldown duration, clock skew leeway and store timeout; defaults are used when null</param>
    /// <param name="tracer">Optional tracer used to export a span for each trip/re-trip/reset transition</param>
    /// <param name="instrumentationOptions">How verbose should the exported spans be</param>
    /// <param name="timeProvider">The clock used to timestamp trips; defaults to <see cref="TimeProvider.System"/></param>
    /// <exception cref="ArgumentNullException">Thrown when the store is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when CooldownDuration is not greater than zero and at most 24 hours,
    /// ClockSkewLeeway is negative or over 24 hours, or StoreTimeout is under 1 second or over 60 seconds</exception>
    public DistributedOutboxCircuitBreaker(
        IAmATrippedTopicStore store,
        OutboxCircuitBreakerOptions? options = null,
        IAmABrighterTracer? tracer = null,
        InstrumentationOptions instrumentationOptions = InstrumentationOptions.All,
        TimeProvider? timeProvider = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        options ??= new OutboxCircuitBreakerOptions();
        _cooldownDuration = ValidatedCooldownDuration(options);
        _clockSkewLeeway = ValidatedClockSkewLeeway(options);
        _storeTimeout = ValidatedStoreTimeout(options);
    }

    private static TimeSpan ValidatedCooldownDuration(OutboxCircuitBreakerOptions options)
    {
        var cooldownDuration = options.CooldownDuration;
        if (cooldownDuration <= TimeSpan.Zero || cooldownDuration > s_longestPermittedDuration)
            throw new ArgumentOutOfRangeException(nameof(options), cooldownDuration,
                "CooldownDuration must be greater than zero and at most 24 hours");

        return cooldownDuration;
    }

    private static TimeSpan ValidatedClockSkewLeeway(OutboxCircuitBreakerOptions options)
    {
        var clockSkewLeeway = options.ClockSkewLeeway;
        if (clockSkewLeeway < TimeSpan.Zero || clockSkewLeeway > s_longestPermittedDuration)
            throw new ArgumentOutOfRangeException(nameof(options), clockSkewLeeway,
                "ClockSkewLeeway must not be negative and at most 24 hours");

        return clockSkewLeeway;
    }

    private static TimeSpan ValidatedStoreTimeout(OutboxCircuitBreakerOptions options)
    {
        var storeTimeout = options.StoreTimeout;
        if (storeTimeout < s_shortestPermittedStoreTimeout || storeTimeout > s_longestPermittedStoreTimeout)
            throw new ArgumentOutOfRangeException(nameof(options), storeTimeout,
                "StoreTimeout must be at least 1 second and at most 60 seconds");

        return storeTimeout;
    }

    /// <inheritdoc />
    public IEnumerable<RoutingKey> TrippedTopics => throw new NotImplementedException();

    /// <inheritdoc />
    public TrippedTopicStoreStatus StoreStatus => throw new NotImplementedException();

    /// <inheritdoc />
    public void CoolDown()
    {
    }

    /// <inheritdoc />
    public void TripTopic(RoutingKey topic) => throw new NotImplementedException();

    /// <inheritdoc />
    public IAmATripBatch BeginTripBatch() => throw new NotImplementedException();

    /// <inheritdoc />
    public Task<IReadOnlyCollection<RoutingKey>> ReadTrippedTopicsAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public void StartRefresh() => throw new NotImplementedException();
}
