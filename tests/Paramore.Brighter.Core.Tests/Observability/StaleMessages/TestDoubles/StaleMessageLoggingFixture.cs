#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel

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

#nullable enable

using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Core.Tests.Observability.StaleMessages.TestDoubles;

/// <summary>
/// Adds one shared <see cref="CapturingLoggerProvider"/> to <c>ApplicationLogging.LoggerFactory</c> for the tests of
/// <see cref="StaleMessageLoggingCollection"/>. The factory cannot remove a provider, so it is added once.
/// </summary>
public sealed class StaleMessageLoggingFixture
{
    /// <summary>Adds the provider to the application's logger factory.</summary>
    public StaleMessageLoggingFixture() => ApplicationLogging.LoggerFactory.AddProvider(Logs);

    /// <summary>The shared provider; each test calls <see cref="CapturingLoggerProvider.Reset"/> before it acts.</summary>
    public CapturingLoggerProvider Logs { get; } = new();
}
