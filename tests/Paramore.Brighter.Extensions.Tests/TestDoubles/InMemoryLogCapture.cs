#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// Captures Brighter logging for tests that run without concurrent factory replacements.
/// </summary>
public sealed class InMemoryLogCapture : IDisposable
{
    private readonly ILoggerFactory _previousFactory = ApplicationLogging.LoggerFactory;
    private readonly CapturingLoggerProvider _provider = new();
    private readonly ILoggerFactory _factory;

    /// <summary>
    /// Installs a factory that writes to the in-memory capture.
    /// </summary>
    public InMemoryLogCapture()
    {
        _factory = LoggerFactory.Create(builder => builder.AddProvider(_provider));
        ApplicationLogging.LoggerFactory = _factory;
    }

    /// <summary>
    /// Gets the entries written while this capture's factory was active.
    /// </summary>
    public IReadOnlyCollection<CapturedLogEntry> Entries => _provider.Entries;

    /// <summary>
    /// Restores the previous application factory and disposes the capture's factory.
    /// </summary>
    public void Dispose()
    {
        ApplicationLogging.LoggerFactory = _previousFactory;
        _factory.Dispose();
    }
}
