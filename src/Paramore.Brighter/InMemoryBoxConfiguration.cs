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

using System;

namespace Paramore.Brighter
{
    /// <summary>
    /// Configuration for the default <see cref="InMemoryOutbox"/> created when no explicit outbox is provided.
    /// </summary>
    public record InMemoryBoxConfiguration
    {
        /// <summary>
        /// How many messages to retain before compaction runs. Use -1 to disable compaction. Defaults to 2048.
        /// </summary>
        public int EntryLimit { get; init; } = 2048;

        /// <summary>
        /// How long a dispatched message lives before expiry removes it. Defaults to 5 minutes.
        /// </summary>
        public TimeSpan EntryTimeToLive { get; init; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Minimum interval between expiry scans. Defaults to 10 minutes.
        /// </summary>
        public TimeSpan ExpirationScanInterval { get; init; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Target size as a percentage of <see cref="EntryLimit"/> after compaction. Defaults to 0.
        /// </summary>
        public double CompactionPercentage { get; init; }

        /// <summary>
        /// Creates a new <see cref="InMemoryBoxConfiguration"/> with the specified values.
        /// </summary>
        public InMemoryBoxConfiguration(
            int EntryLimit = 2048,
            TimeSpan? EntryTimeToLive = null,
            TimeSpan? ExpirationScanInterval = null,
            double CompactionPercentage = 0)
        {
            this.EntryLimit = EntryLimit;
            this.EntryTimeToLive = EntryTimeToLive ?? TimeSpan.FromMinutes(5);
            this.ExpirationScanInterval = ExpirationScanInterval ?? TimeSpan.FromMinutes(10);
            this.CompactionPercentage = CompactionPercentage;
        }

        /// <summary>
        /// Creates a new <see cref="InMemoryBoxConfiguration"/> with default values.
        /// </summary>
        public InMemoryBoxConfiguration() { }
    }
}
