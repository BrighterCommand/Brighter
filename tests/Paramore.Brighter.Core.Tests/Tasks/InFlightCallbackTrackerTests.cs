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
using Xunit;

namespace Paramore.Brighter.Core.Tests.Tasks;

public class InFlightCallbackTrackerTests
{
    [Fact]
    public void When_nothing_in_flight_TryWait_returns_true_immediately()
    {
        var tracker = new InFlightCallbackTracker();

        Assert.True(tracker.TryWait(TimeSpan.Zero, out int stillInFlight));
        Assert.Equal(0, stillInFlight);
    }

    [Fact]
    public async Task When_callbacks_in_flight_TryWait_blocks_until_the_last_End()
    {
        var tracker = new InFlightCallbackTracker();
        tracker.Begin();
        tracker.Begin();

        var wait = Task.Run(() => tracker.TryWait(TimeSpan.FromSeconds(5), out _));

        await Assert.ThrowsAsync<TimeoutException>(
            async () => await wait.WaitAsync(TimeSpan.FromMilliseconds(100)));

        tracker.End();
        await Assert.ThrowsAsync<TimeoutException>(
            async () => await wait.WaitAsync(TimeSpan.FromMilliseconds(100)));

        tracker.End();
        Assert.True(await wait.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void When_the_timeout_elapses_TryWait_returns_false_with_the_remaining_count()
    {
        var tracker = new InFlightCallbackTracker();
        tracker.Begin();
        tracker.Begin();
        tracker.Begin();

        Assert.False(tracker.TryWait(TimeSpan.FromMilliseconds(50), out int stillInFlight));
        Assert.Equal(3, stillInFlight);
    }

    [Fact]
    public async Task When_a_drain_has_completed_the_tracker_is_reusable()
    {
        var tracker = new InFlightCallbackTracker();

        tracker.Begin();
        tracker.End();
        Assert.True(tracker.TryWait(TimeSpan.Zero, out _));

        tracker.Begin();
        var wait = Task.Run(() => tracker.TryWait(TimeSpan.FromSeconds(5), out _));
        await Assert.ThrowsAsync<TimeoutException>(
            async () => await wait.WaitAsync(TimeSpan.FromMilliseconds(100)));

        tracker.End();
        Assert.True(await wait.WaitAsync(TimeSpan.FromSeconds(1)));
    }
}
