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

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;

public sealed class InMemoryServiceBusLogProvider : ILoggerProvider
{
    public ConcurrentQueue<(LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Properties)> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(this);
    public void Dispose() { }

    private sealed class InMemoryLogger(InMemoryServiceBusLogProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, object?>();
            provider.Entries.Enqueue((logLevel, formatter(state, exception), properties));
        }
    }
}
