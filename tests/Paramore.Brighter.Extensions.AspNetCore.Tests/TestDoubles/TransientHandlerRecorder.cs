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
/// Records the three <see cref="IMarker"/> instances AC-47's scenario cares about: the controller's own
/// request-scoped instance (<c>R</c>), the one resolved by the nested <c>Post</c> a <c>Send</c> handler
/// issues from inside its own <c>Handle</c>, and the one resolved by the nested <c>Post</c> a
/// <c>Publish</c> subscriber issues from inside its own <c>HandleAsync</c>. Register as a singleton in
/// the container under test.
/// </summary>
public sealed class TransientHandlerRecorder
{
    /// <summary>
    /// The controller's own <see cref="IMarker"/> - the request-scoped instance <c>R</c>.
    /// </summary>
    public IMarker? RequestScopeInstance { get; private set; }

    /// <summary>
    /// The <see cref="IMarker"/> resolved by the nested <c>Post</c> the <c>Send</c> handler issues.
    /// </summary>
    public IMarker? SendNestedInstance { get; private set; }

    /// <summary>
    /// The <see cref="IMarker"/> resolved by the nested <c>Post</c> the <c>Publish</c> subscriber issues.
    /// </summary>
    public IMarker? PublishNestedInstance { get; private set; }

    public void RecordRequestScopeInstance(IMarker marker) => RequestScopeInstance = marker;

    public void RecordSendNestedInstance(IMarker marker) => SendNestedInstance = marker;

    public void RecordPublishNestedInstance(IMarker marker) => PublishNestedInstance = marker;
}
