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
/// Records what <see cref="FailedBuildController"/> observed while repeatedly <c>Post</c>ing a command
/// whose mapper can never be built, and the state of its own <see cref="IOrderDbContext"/> immediately
/// afterwards - before the HTTP response, and therefore before ASP.NET could have disposed the request
/// scope (AC-38). Register as a singleton in the container under test.
/// </summary>
public sealed class FailedBuildRecorder
{
    /// <summary>
    /// How many of the <c>Post</c> attempts threw the expected <see cref="ConfigurationException"/>
    /// wrapping an <see cref="System.InvalidOperationException"/>.
    /// </summary>
    public int ExpectedFailureCount { get; private set; }

    /// <summary>
    /// How many <c>Post</c> attempts produced anything other than the expected failure - either no
    /// exception at all, or an exception of the wrong shape.
    /// </summary>
    public int UnexpectedOutcomeCount { get; private set; }

    /// <summary>
    /// Whether the controller's own <see cref="IOrderDbContext"/> was still usable immediately after the
    /// failed <c>Post</c> attempts.
    /// </summary>
    public bool RequestScopeUsableAfterFailures { get; private set; }

    /// <summary>
    /// The controller's own <see cref="IOrderDbContext"/>'s dispose count, sampled immediately after the
    /// failed <c>Post</c> attempts.
    /// </summary>
    public int OrderDbContextDisposeCountAfterFailures { get; private set; }

    public void RecordExpectedFailure() => ExpectedFailureCount++;

    public void RecordUnexpectedOutcome() => UnexpectedOutcomeCount++;

    public void RecordRequestScopeStateAfterFailures(bool usable, int disposeCount)
    {
        RequestScopeUsableAfterFailures = usable;
        OrderDbContextDisposeCountAfterFailures = disposeCount;
    }
}
