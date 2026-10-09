#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.Observability;

/// <summary>What initiated an Outbox clear operation.</summary>
public enum OutboxClearSource
{
    /// <summary>The clear was initiated explicitly by the application (e.g. <c>ClearOutbox</c> / <c>ClearOutboxAsync</c>).</summary>
    Explicit,

    /// <summary>The clear was initiated by the background Outbox sweeper (<c>ClearOutstandingFromOutboxAsync</c>).</summary>
    Sweeper,

    /// <summary>
    /// The clear source could not be determined. This occurs in three cases:
    /// <list type="bullet">
    ///   <item>The confirmation callback received a <c>PublishConfirmationResult</c> with an empty <c>MessageId</c>.</item>
    ///   <item>The pending-clear entry for the message was evicted by the TTL (5 minutes by default).</item>
    ///   <item>The pending-clear map was at its hard cap (10,000 entries) when the message was dispatched.</item>
    /// </list>
    /// </summary>
    Unknown
}

/// <summary>Extension methods for mapping <see cref="OutboxClearSource"/> to its attribute string value.</summary>
public static class OutboxClearSourceExtensions
{
    /// <summary>
    /// Returns the bounded attribute string value for the given <paramref name="clearSource"/>.
    /// An unmapped value returns <c>"unknown"</c> without throwing, so a metrics path degrades rather than faults.
    /// </summary>
    public static string ToClearSourceName(this OutboxClearSource clearSource) => clearSource switch
    {
        OutboxClearSource.Explicit => "explicit",
        OutboxClearSource.Sweeper  => "sweeper",
        _                          => "unknown"
    };
}
