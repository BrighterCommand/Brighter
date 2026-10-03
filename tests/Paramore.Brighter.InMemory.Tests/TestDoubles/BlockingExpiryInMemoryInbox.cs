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
using System.Threading;

namespace Paramore.Brighter.InMemory.Tests.TestDoubles
{
    /// <summary>
    /// An <see cref="InMemoryInbox"/> whose expiry scan, once armed, blocks while it holds the cleanup lock.
    /// Lets a test deterministically hold the lock that compaction also needs.
    /// </summary>
    internal sealed class BlockingExpiryInMemoryInbox(TimeProvider timeProvider) : InMemoryInbox(timeProvider)
    {
        private volatile bool _armed;

        public ManualResetEventSlim ExpiryEntered { get; } = new(false);
        public ManualResetEventSlim ReleaseExpiry { get; } = new(false);

        public void ArmExpiryBlock() => _armed = true;

        protected override void RemoveExpiredMessages(DateTimeOffset now)
        {
            if (_armed)
            {
                ExpiryEntered.Set();
                ReleaseExpiry.Wait(TimeSpan.FromSeconds(10));
            }

            base.RemoveExpiredMessages(now);
        }
    }
}
