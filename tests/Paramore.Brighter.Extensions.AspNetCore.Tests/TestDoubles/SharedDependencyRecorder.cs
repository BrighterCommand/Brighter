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

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// Records the <see cref="IMarker"/> a mapper and a handler each resolved for the same request (AC-34),
/// and the shared instance's dispose count as observed mid-action, so a test can compare that against the
/// count once the whole HTTP request has completed. Register as a singleton in the container under test.
/// </summary>
public sealed class SharedDependencyRecorder
{
    /// <summary>
    /// The <see cref="IMarker"/> the mapper was constructed with.
    /// </summary>
    public IMarker? MapperMarker { get; private set; }

    /// <summary>
    /// The <see cref="IMarker"/> the handler was constructed with.
    /// </summary>
    public IMarker? HandlerMarker { get; private set; }

    /// <summary>
    /// The shared <see cref="IMarker"/>'s own dispose count, as observed immediately after the action's
    /// <c>Send</c> call returned - before the HTTP response, and therefore before ASP.NET could have
    /// disposed the request scope.
    /// </summary>
    public int DisposeCountAfterAction { get; private set; }

    /// <summary>
    /// Records the mapper's own <see cref="IMarker"/>.
    /// </summary>
    public void RecordMapper(IMarker marker) => MapperMarker = marker;

    /// <summary>
    /// Records the handler's own <see cref="IMarker"/>.
    /// </summary>
    public void RecordHandler(IMarker marker) => HandlerMarker = marker;

    /// <summary>
    /// Records the shared <see cref="IMarker"/>'s dispose count as observed immediately after the action's
    /// <c>Send</c> call returned.
    /// </summary>
    public void RecordDisposeCountAfterAction(int disposeCount) => DisposeCountAfterAction = disposeCount;
}
