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

namespace Paramore.Brighter.CircuitBreaker;

/// <summary>
/// A store that shares trips between the instances of a deployment.
/// </summary>
public interface IAmATrippedTopicStore
{
    /// <summary>
    /// Reads the trips that are still live.
    /// </summary>
    /// <param name="now">The reader's clock.</param>
    /// <param name="tripLength">How long a trip lasts.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>The live trips.</returns>
    Task<TrippedTopicStoreRead> ReadAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the trips that have run out as ended and returns them.
    /// </summary>
    /// <remarks>
    /// Each trip that is not yet marked and whose stamp is at or before <paramref name="now"/> less <paramref name="tripLength"/> is marked ended
    /// and returned to exactly one caller, however many callers race for it.
    /// </remarks>
    /// <param name="now">The caller's clock.</param>
    /// <param name="tripLength">How long a trip lasts.</param>
    /// <param name="cancellationToken">A token that cancels the call.</param>
    /// <returns>The trips this call marked ended.</returns>
    Task<IReadOnlyList<TopicTrip>> EndExpiredAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the trips that are live, changing nothing.
    /// </summary>
    /// <param name="now">The reader's clock.</param>
    /// <param name="tripLength">How long a trip lasts.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>The trips that are not marked ended and have not run out.</returns>
    Task<IReadOnlyList<TopicTrip>> ReadLiveAsync(DateTimeOffset now, TimeSpan tripLength, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes trips, keeping the later of the stored and the written stamp for each topic.
    /// </summary>
    /// <param name="trips">The trips to write, with distinct topics.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <returns>One result per trip.</returns>
    Task<IReadOnlyList<TripWriteResult>> WriteAsync(IReadOnlyList<TopicTrip> trips, CancellationToken cancellationToken = default);
}
