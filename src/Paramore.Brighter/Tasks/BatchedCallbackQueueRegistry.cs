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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Paramore.Brighter.Tasks
{
    /// <summary>
    /// The live <see cref="BatchedCallbackQueue"/>s whose depth is reported. Producers are built outside DI, so a
    /// queue registers itself here and a meter reads the registry when its instrument is collected. It holds weak
    /// references, so a queue that is never completed still goes when it is collected.
    /// </summary>
    internal static class BatchedCallbackQueueRegistry
    {
        private static readonly ConcurrentDictionary<long, WeakReference<BatchedCallbackQueue>> s_queues = new();
        private static long s_nextId;

        /// <summary>Registers a queue and returns the id that unregisters it.</summary>
        public static long Register(BatchedCallbackQueue queue)
        {
            var id = Interlocked.Increment(ref s_nextId);
            s_queues[id] = new WeakReference<BatchedCallbackQueue>(queue);
            return id;
        }

        public static void Unregister(long id) => s_queues.TryRemove(id, out _);

        public static IEnumerable<BatchedCallbackQueue> LiveQueues()
        {
            foreach (var entry in s_queues)
            {
                if (entry.Value.TryGetTarget(out var queue))
                    yield return queue;
                else
                    s_queues.TryRemove(entry.Key, out _);
            }
        }
    }
}
