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

using System.Collections.Generic;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    /// <summary>
    /// Records handler release and pipeline scope disposal on a shared, ordered log, so a test can
    /// assert not just that every handler was released but that the pipeline scope's own disposal
    /// happened last, after every release.
    /// </summary>
    public sealed class ReleaseOrderRecorder
    {
        private readonly List<string> _events = new();

        /// <summary>The events in the order they happened, e.g. <c>["Released:A", "Released:B", "ScopeDisposed"]</c>.</summary>
        public IReadOnlyList<string> Events => _events;

        /// <summary>Records that the handler labelled <paramref name="label"/> was released.</summary>
        public void RecordRelease(string label) => _events.Add($"Released:{label}");

        /// <summary>Records that the pipeline scope handle was disposed.</summary>
        public void RecordScopeDisposed() => _events.Add("ScopeDisposed");
    }
}
