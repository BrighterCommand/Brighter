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

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// A handler for <see cref="HandlerReleaseThrowsCommand"/> that records whether it ran, and optionally
/// throws, so a test can distinguish "the caller observed the handler's own exception" from "the caller
/// observed the handler factory's <c>Release</c> failure" (AC-51).
/// </summary>
public sealed class RecordingRequestHandler : RequestHandler<HandlerReleaseThrowsCommand>
{
    private readonly bool _throwOnHandle;

    public RecordingRequestHandler(bool throwOnHandle = false) => _throwOnHandle = throwOnHandle;

    /// <summary>Whether <see cref="Handle"/> ran to completion for this instance.</summary>
    public bool Completed { get; private set; }

    public override HandlerReleaseThrowsCommand Handle(HandlerReleaseThrowsCommand command)
    {
        if (_throwOnHandle)
            throw new InvalidOperationException("Handle failed.");

        Completed = true;
        return base.Handle(command);
    }
}
