#region License

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.CircuitBreaker;

/// <summary>
/// A store that shares trips between the instances of a deployment.
/// </summary>
public interface IAmATrippedTopicStore
{
    /// <summary>
    /// Marks the trips that have run out as ended and returns them.
    /// </summary>
    /// <remarks>
    /// Each trip that is not yet marked and whose stamp is at or before the <paramref name="window"/>'s <see cref="TripWindow.Cutoff"/> is marked ended
    /// and returned to exactly one caller, however many callers race for it.
    /// </remarks>
    /// <param name="window">The caller's clock and how long a trip lasts.</param>
    /// <param name="cancellationToken">A token that cancels the call.</param>
    /// <returns>The trips this call marked ended.</returns>
    Task<IReadOnlyList<TopicTrip>> EndExpiredAsync(TripWindow window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the trips that are live, changing nothing.
    /// </summary>
    /// <param name="window">The reader's clock and how long a trip lasts.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>The trips that are not marked ended and have not run out.</returns>
    Task<IReadOnlyList<TopicTrip>> ReadLiveAsync(TripWindow window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes trips, keeping the later of the stored and the written stamp for each topic.
    /// </summary>
    /// <param name="trips">The trips to write, with distinct topics.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>One result per trip.</returns>
    Task<IReadOnlyList<TripWriteResult>> WriteAsync(IReadOnlyList<TopicTrip> trips, CancellationToken cancellationToken = default);
}
