#region Licence
/* The MIT License (MIT)
Copyright © 2026 Tom Longhurst

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

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

public sealed class CountingLoggerFactory : ILoggerFactory
{
    private readonly ILoggerFactory _inner = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace));
    public ConcurrentDictionary<string, int> Creations { get; } = new();

    public ILogger CreateLogger(string categoryName)
    {
        Creations.AddOrUpdate(categoryName, 1, (_, count) => count + 1);
        return _inner.CreateLogger(categoryName);
    }

    public void AddProvider(ILoggerProvider provider) => _inner.AddProvider(provider);
    public void Dispose() => _inner.Dispose();
}
