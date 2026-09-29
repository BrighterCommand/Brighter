#region Licence

/* The MIT License (MIT)
Copyright © 2026 Tom Longhurst <30480171+thomhurst@users.noreply.github.com>

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
using System.Threading.Tasks;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.Core.Tests.Tasks;

public class InFlightCallbackTrackerTests
{
    [Test]
    public async System.Threading.Tasks.Task When_nothing_in_flight_TryWait_returns_true_immediately()
    {
        var tracker = new InFlightCallbackTracker();

        await Assert.That(tracker.TryWait(TimeSpan.Zero, out int stillInFlight)).IsTrue();
        await Assert.That(stillInFlight).IsEqualTo(0);
    }

    [Test]
    public async Task When_callbacks_in_flight_TryWait_blocks_until_the_last_End()
    {
        var tracker = new InFlightCallbackTracker();
        tracker.Begin();
        tracker.Begin();

        var wait = Task.Run(() => tracker.TryWait(TimeSpan.FromSeconds(5), out _));

        await Assert.That(async () => await wait.WaitAsync(TimeSpan.FromMilliseconds(100))).ThrowsExactly<TimeoutException>();

        tracker.End();
        await Assert.That(async () => await wait.WaitAsync(TimeSpan.FromMilliseconds(100))).ThrowsExactly<TimeoutException>();

        tracker.End();
        await Assert.That(await wait.WaitAsync(TimeSpan.FromSeconds(1))).IsTrue();
    }

    [Test]
    public async System.Threading.Tasks.Task When_the_timeout_elapses_TryWait_returns_false_with_the_remaining_count()
    {
        var tracker = new InFlightCallbackTracker();
        tracker.Begin();
        tracker.Begin();
        tracker.Begin();

        await Assert.That(tracker.TryWait(TimeSpan.FromMilliseconds(50), out int stillInFlight)).IsFalse();
        await Assert.That(stillInFlight).IsEqualTo(3);
    }

    [Test]
    public async Task When_a_drain_has_completed_the_tracker_is_reusable()
    {
        var tracker = new InFlightCallbackTracker();

        tracker.Begin();
        tracker.End();
        await Assert.That(tracker.TryWait(TimeSpan.Zero, out _)).IsTrue();

        tracker.Begin();
        var wait = Task.Run(() => tracker.TryWait(TimeSpan.FromSeconds(5), out _));
        await Assert.That(async () => await wait.WaitAsync(TimeSpan.FromMilliseconds(100))).ThrowsExactly<TimeoutException>();

        tracker.End();
        await Assert.That(await wait.WaitAsync(TimeSpan.FromSeconds(1))).IsTrue();
    }
}
