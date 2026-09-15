#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections.Generic;
using System.Threading;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway;

/// <summary>
/// Behaviours of the shared conformance harness scheduler, exercised without a broker.
/// </summary>
/// <remarks>
/// <para>
/// The scheduler is generated from one <c>Shared/</c> template into every configured gateway, so
/// every copy is the same code and proving it once proves it everywhere. Redis hosts the proof
/// because its generated types share a namespace with its provider and it configures a single
/// gateway, so there is exactly one copy here to name.
/// </para>
/// <para>
/// No broker is involved: the republish action is supplied by the caller, so these tests drive the
/// scheduler's own machinery - timers, identity and disposal - and nothing else.
/// </para>
/// </remarks>
public class ConformanceHarnessMessageSchedulerTests
{
    private static readonly TimeSpan DELAY = TimeSpan.FromMilliseconds(200);

    // Long enough that an uncancelled timer would certainly have fired, short enough to stay cheap.
    private static readonly TimeSpan WELL_PAST_THE_DELAY = TimeSpan.FromSeconds(2);

    [Fact]
    public void When_cancelling_a_scheduled_message_should_not_republish_it()
    {
        // Arrange
        var republished = new ManualResetEventSlim(false);
        using var scheduler = new ConformanceHarnessMessageScheduler(_ =>
        {
            republished.Set();
            return null;
        });

        var schedulerId = scheduler.Schedule(AMessage(), DELAY);

        // Act
        scheduler.Cancel(schedulerId);

        // Assert — a cancelled schedule never fires. Asserting an absence, so this is a single
        // bounded wait rather than a retry: waiting for it to happen would invert the assertion.
        Assert.False(republished.Wait(WELL_PAST_THE_DELAY),
            "Cancel was given the id Schedule returned, so the message should never have been "
            + "republished. A Cancel that cannot find its timer is a silent no-op: the caller is "
            + "told nothing, and the message arrives anyway.");
    }

    [Fact]
    public void When_a_schedule_is_not_cancelled_should_republish_it()
    {
        // Arrange — the other half of the same claim. Without this, a Cancel that disabled the
        // scheduler entirely, or a Schedule that never armed a timer, would pass the test above.
        var republished = new ManualResetEventSlim(false);
        using var scheduler = new ConformanceHarnessMessageScheduler(_ =>
        {
            republished.Set();
            return null;
        });

        // Act
        scheduler.Schedule(AMessage(), DELAY);

        // Assert
        Assert.True(republished.Wait(WELL_PAST_THE_DELAY),
            "an uncancelled schedule should republish once its delay elapses.");
    }

    [Fact]
    public void When_cancelling_one_of_two_schedules_should_republish_only_the_other()
    {
        // Arrange — Cancel must find the one schedule it was given, not all of them and not none
        var republished = new List<string>();
        var second = new ManualResetEventSlim(false);
        using var scheduler = new ConformanceHarnessMessageScheduler(message =>
        {
            lock (republished)
            {
                republished.Add(message.Header.Topic.Value);
            }

            if (message.Header.Topic.Value == "kept")
            {
                second.Set();
            }

            return null;
        });

        var cancelled = scheduler.Schedule(AMessage("cancelled"), DELAY);
        scheduler.Schedule(AMessage("kept"), DELAY);

        // Act
        scheduler.Cancel(cancelled);

        // Assert
        Assert.True(second.Wait(WELL_PAST_THE_DELAY), "the schedule that was not cancelled should fire.");

        lock (republished)
        {
            Assert.Equal(new[] { "kept" }, republished);
        }
    }

    [Fact]
    public void When_disposing_with_a_republish_in_flight_should_dispose_what_it_allocated()
    {
        // Arrange - a republish that has started but not returned when Dispose runs. Publishing
        // allocates a producer holding a broker connection, and the scheduler is the only thing
        // that knows to close it; the callback is the one path where the scheduler can be torn
        // down between allocating that producer and being told about it.
        var inFlight = new ManualResetEventSlim(false);
        var release = new ManualResetEventSlim(false);
        var allocated = new TrackingDisposable();

        var scheduler = new ConformanceHarnessMessageScheduler(_ =>
        {
            inFlight.Set();
            release.Wait(WELL_PAST_THE_DELAY);
            return allocated;
        });

        scheduler.Schedule(AMessage(), TimeSpan.Zero);
        Assert.True(inFlight.Wait(WELL_PAST_THE_DELAY), "the republish should have begun.");

        // Act - tear the scheduler down with the republish still inside the delegate, then let it
        // return. Nothing may block: Dispose must not wait on a callback that is waiting on us.
        scheduler.Dispose();
        release.Set();

        // Assert
        Assert.True(allocated.Disposed.Wait(WELL_PAST_THE_DELAY),
            "a republish that returned after Dispose had run handed its producer to a list that "
            + "will never be walked again. Nothing else holds it, so the connection it opened stays "
            + "open past the end of the test that opened it.");
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static Message AMessage(string topic = "conformance.harness.scheduler") =>
        new(
            new MessageHeader(Id.Random(), new RoutingKey(topic), MessageType.MT_COMMAND),
            new MessageBody("{}"));

    /// <summary>
    /// Stands in for whatever republishing allocated - a producer, a producer registry - and
    /// records that the scheduler closed it.
    /// </summary>
    private sealed class TrackingDisposable : IDisposable
    {
        public ManualResetEventSlim Disposed { get; } = new(false);

        public void Dispose() => Disposed.Set();
    }
}
