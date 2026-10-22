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

#nullable enable

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.Hosting;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Registers a directly driven <see cref="OutboxSweeperState"/> for the sweeper health checks, without starting a hosted sweeper
/// that would write the same record. The record's scenarios are 31 s of a hung sweep, 3 consecutive failed sweeps and an active sweeper.
/// </summary>
public static class OutboxSweeperRecordScenario
{
    /// <summary>The sweep duration that exceeds a 30 s hang threshold.</summary>
    public static readonly TimeSpan HungSweepRunningTime = TimeSpan.FromSeconds(31);

    /// <summary>The consecutive failed sweeps that reach a failure threshold of 3.</summary>
    public const int ConsecutiveFailures = 3;

    /// <summary>The timer interval, in seconds, registered in the sweeper options.</summary>
    public const int TimerIntervalSeconds = 10;

    /// <summary>
    /// Registers a record on which nothing has happened, with the sweeper options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="clock">The clock the record is timed on.</param>
    /// <returns>The registered record.</returns>
    public static OutboxSweeperState Register(IServiceCollection services, FakeTimeProvider clock)
    {
        var state = new OutboxSweeperState(clock);
        services.AddSingleton(state);
        services.AddSingleton(new TimedOutboxSweeperOptions { TimerInterval = TimerIntervalSeconds });
        return state;
    }

    /// <summary>
    /// Registers a record holding a sweep that has held the lock for 31 s and is still running.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="clock">The clock the record is timed on.</param>
    /// <returns>The registered record.</returns>
    public static OutboxSweeperState RegisterHungSweep(IServiceCollection services, FakeTimeProvider clock)
    {
        var state = Register(services, clock);
        state.SweepStarted();
        state.LockObtained();
        clock.Advance(HungSweepRunningTime);
        return state;
    }

    /// <summary>
    /// Registers a record whose last 3 sweeps all failed.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="clock">The clock the record is timed on.</param>
    /// <returns>The registered record.</returns>
    public static OutboxSweeperState RegisterConsecutiveFailures(IServiceCollection services, FakeTimeProvider clock)
    {
        var state = Register(services, clock);
        for (var sweep = 0; sweep < ConsecutiveFailures; sweep++)
        {
            state.SweepStarted();
            state.LockObtained();
            state.SweepEnded(SweepOutcome.Failed);
        }

        return state;
    }

    /// <summary>
    /// Registers a record for an active sweeper: it has completed a sweep that held the lock.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="clock">The clock the record is timed on.</param>
    /// <returns>The registered record.</returns>
    public static OutboxSweeperState RegisterActiveSweeper(IServiceCollection services, FakeTimeProvider clock)
    {
        var state = Register(services, clock);
        state.SweepStarted();
        state.LockObtained();
        state.SweepEnded(SweepOutcome.Completed);
        return state;
    }

    /// <summary>
    /// Registers a real in-memory circuit breaker with the given topics tripped.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="topics">The topics to trip.</param>
    /// <returns>The registered circuit breaker.</returns>
    public static InMemoryOutboxCircuitBreaker RegisterTrippedTopics(IServiceCollection services, params string[] topics)
    {
        var circuitBreaker = new InMemoryOutboxCircuitBreaker();
        foreach (var topic in topics)
            circuitBreaker.TripTopic(new RoutingKey(topic));

        services.AddSingleton<IAmAnOutboxCircuitBreaker>(circuitBreaker);
        return circuitBreaker;
    }
}
