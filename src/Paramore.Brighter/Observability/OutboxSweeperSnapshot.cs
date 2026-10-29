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
/// A consistent, point-in-time copy of an <see cref="OutboxSweeperState"/>.
/// </summary>
/// <param name="IsSweepInProgress">A sweep has started and not ended.</param>
/// <param name="RunningTime">Time since the sweep started, or <see cref="TimeSpan.Zero"/> when no sweep is in progress.</param>
/// <param name="HoldsLock">The sweep in progress holds the lock.</param>
/// <param name="ConsecutiveFailedSweeps">Sweeps that have failed in a row, up to and including the latest.</param>
/// <param name="LastOutcome">Outcome of the most recent ended sweep, or null before the first sweep ends.</param>
/// <param name="IsStopped">A stop has been requested.</param>
/// <param name="IsActive">The sweeper is actively sweeping, as decided by the <see cref="OutboxSweeperState"/>.</param>
public readonly record struct OutboxSweeperSnapshot(
    bool IsSweepInProgress,
    TimeSpan RunningTime,
    bool HoldsLock,
    int ConsecutiveFailedSweeps,
    SweepOutcome? LastOutcome,
    bool IsStopped,
    bool IsActive);
