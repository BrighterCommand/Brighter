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
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// A handler for <see cref="ThrowingHandlerCommand"/> that resolves a <see cref="CountingDisposable"/>,
/// records it into an injected <see cref="CountingDisposableRecorder"/> so a test can inspect its
/// disposal count after the pipeline has torn down, then unconditionally throws
/// <see cref="InvalidOperationException"/> from <see cref="HandleAsync"/>.
/// </summary>
public sealed class ThrowingHandlerCommandHandlerAsync : RequestHandlerAsync<ThrowingHandlerCommand>
{
    /// <summary>
    /// The message carried by the exception this handler throws, so a test can assert the exception the
    /// caller observes is this one, unchanged, rather than a replacement or a wrapper.
    /// </summary>
    public const string FailureMessage = "ThrowingHandlerCommandHandlerAsync always fails.";

    public ThrowingHandlerCommandHandlerAsync(CountingDisposable dependency, CountingDisposableRecorder recorder)
    {
        recorder.Record(dependency);
    }

    public override Task<ThrowingHandlerCommand> HandleAsync(ThrowingHandlerCommand command, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(FailureMessage);
    }
}
