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

namespace Paramore.Brighter.Observability;

/// <summary>
/// Holds what an outbox sweeper is doing, so that gauges and health checks can read it without waiting on sweep work.
/// </summary>
/// <remarks>
/// The sweeper is the only intended writer. Readers take a <see cref="OutboxSweeperSnapshot"/> with <see cref="Read"/>.
/// </remarks>
public sealed class OutboxSweeperState
{
    private readonly TimeProvider _clock;
    private readonly object _lock = new();

    /// <summary>
    /// Creates a state with nothing recorded.
    /// </summary>
    /// <param name="clock">The sweeper's clock.</param>
    /// <exception cref="ArgumentNullException">The clock is null.</exception>
    public OutboxSweeperState(TimeProvider clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// Copies the current state.
    /// </summary>
    /// <returns>A consistent snapshot.</returns>
    public OutboxSweeperSnapshot Read()
    {
        lock (_lock)
        {
            return new OutboxSweeperSnapshot(false, TimeSpan.Zero, false, 0, null, false, false);
        }
    }
}
