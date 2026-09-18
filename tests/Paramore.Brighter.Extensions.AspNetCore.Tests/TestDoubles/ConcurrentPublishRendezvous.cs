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

using System;
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// A two-party rendezvous carried on a <see cref="ConcurrentPublishOrderPlaced"/> event, so its two
/// subscribers can prove their executions genuinely overlapped rather than merely both completing
/// without error: each side signals its own arrival, then waits for the other to arrive too. If
/// <c>PublishAsync</c> ever ran the subscribers one at a time instead of concurrently, the first to
/// arrive would have nobody left to wait for and this would time out rather than hang the test.
/// </summary>
public sealed class ConcurrentPublishRendezvous
{
    private readonly TaskCompletionSource _subscriberOneArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _subscriberTwoArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Signals that the first subscriber has arrived, then waits for the second to arrive too.
    /// </summary>
    public Task SubscriberOneArrivedAndWaitForTwoAsync(TimeSpan timeout)
    {
        _subscriberOneArrived.TrySetResult();
        return WaitOrTimeoutAsync(_subscriberTwoArrived.Task, timeout);
    }

    /// <summary>
    /// Signals that the second subscriber has arrived, then waits for the first to arrive too.
    /// </summary>
    public Task SubscriberTwoArrivedAndWaitForOneAsync(TimeSpan timeout)
    {
        _subscriberTwoArrived.TrySetResult();
        return WaitOrTimeoutAsync(_subscriberOneArrived.Task, timeout);
    }

    private static async Task WaitOrTimeoutAsync(Task task, TimeSpan timeout)
    {
        var winner = await Task.WhenAny(task, Task.Delay(timeout));
        if (winner != task)
            throw new TimeoutException(
                "Timed out waiting for the other Publish subscriber to arrive - they did not run concurrently.");
    }
}
