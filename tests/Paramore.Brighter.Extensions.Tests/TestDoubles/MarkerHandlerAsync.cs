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

using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// A handler for <see cref="MarkerCommand"/> that, on entry to <see cref="HandleAsync"/>, records the
/// <see cref="IMarker"/> it was resolved with — together with whether the most recently-recorded
/// <see cref="MarkerTransform"/> marker was already disposed at that moment — via an injected
/// <see cref="MarkerLog"/>. Lets a test assert the handler's <see cref="IMarker"/> is not the transform's
/// (they resolve from different DI scopes), and that the transform's scope had already ended by the time
/// the handler pipeline began.
/// </summary>
public sealed class MarkerHandlerAsync : RequestHandlerAsync<MarkerCommand>
{
    private readonly IMarker _marker;
    private readonly MarkerLog _log;

    public MarkerHandlerAsync(IMarker marker, MarkerLog log)
    {
        _marker = marker;
        _log = log;
    }

    public override async Task<MarkerCommand> HandleAsync(MarkerCommand command, CancellationToken cancellationToken = default)
    {
        var transformMarkers = _log.TransformMarkers;
        var transformDisposedAtEntry = transformMarkers.Count > 0 && transformMarkers[^1].IsDisposed;
        _log.RecordHandler(_marker, transformDisposedAtEntry);

        return await base.HandleAsync(command, cancellationToken);
    }
}
