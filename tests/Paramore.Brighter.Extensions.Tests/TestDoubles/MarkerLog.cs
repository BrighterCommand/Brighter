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

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// Records, in resolution order, the <see cref="IMarker"/> a <see cref="MarkerMapper"/> and a
/// <see cref="MarkerTransform"/> were each constructed with, so a test can assert the two shared one
/// instance within a pipeline and that a later pipeline was resolved a different one. Register as a
/// singleton in the container under test and inject into both doubles' constructors.
/// </summary>
public sealed class MarkerLog
{
    private readonly List<IMarker> _mapperMarkers = new();
    private readonly List<IMarker> _transformMarkers = new();
    private readonly List<IMarker> _handlerMarkers = new();
    private readonly List<bool> _transformDisposedAtHandlerEntry = new();

    /// <summary>
    /// The <see cref="IMarker"/> each <see cref="MarkerMapper"/> construction recorded, in order.
    /// </summary>
    public IReadOnlyList<IMarker> MapperMarkers
    {
        get { lock (_mapperMarkers) return _mapperMarkers.ToArray(); }
    }

    /// <summary>
    /// The <see cref="IMarker"/> each <see cref="MarkerTransform"/> construction recorded, in order.
    /// </summary>
    public IReadOnlyList<IMarker> TransformMarkers
    {
        get { lock (_transformMarkers) return _transformMarkers.ToArray(); }
    }

    /// <summary>
    /// The <see cref="IMarker"/> each handler's <c>Handle</c>/<c>HandleAsync</c> entry recorded, in
    /// order.
    /// </summary>
    public IReadOnlyList<IMarker> HandlerMarkers
    {
        get { lock (_handlerMarkers) return _handlerMarkers.ToArray(); }
    }

    /// <summary>
    /// For each handler entry recorded, whether the most recently-recorded <see cref="MarkerTransform"/>
    /// marker was already disposed at that moment, in the same order as <see cref="HandlerMarkers"/>.
    /// </summary>
    public IReadOnlyList<bool> TransformDisposedAtHandlerEntry
    {
        get { lock (_transformDisposedAtHandlerEntry) return _transformDisposedAtHandlerEntry.ToArray(); }
    }

    /// <summary>
    /// Records the <see cref="IMarker"/> a <see cref="MarkerMapper"/> was constructed with.
    /// </summary>
    public void RecordMapper(IMarker marker)
    {
        lock (_mapperMarkers) _mapperMarkers.Add(marker);
    }

    /// <summary>
    /// Records the <see cref="IMarker"/> a <see cref="MarkerTransform"/> was constructed with.
    /// </summary>
    public void RecordTransform(IMarker marker)
    {
        lock (_transformMarkers) _transformMarkers.Add(marker);
    }

    /// <summary>
    /// Records the <see cref="IMarker"/> a handler resolved on entry to <c>Handle</c>/<c>HandleAsync</c>,
    /// together with whether the most recently-recorded transform marker was disposed at that point.
    /// </summary>
    public void RecordHandler(IMarker marker, bool transformDisposedAtEntry)
    {
        lock (_handlerMarkers) _handlerMarkers.Add(marker);
        lock (_transformDisposedAtHandlerEntry) _transformDisposedAtHandlerEntry.Add(transformDisposedAtEntry);
    }
}
