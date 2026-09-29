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

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Paramore.Brighter;

/// <summary>Reuses category loggers without extending the lifetime of their owning factory.</summary>
public static class BrighterLoggerFactoryExtensions
{
    /// <summary>Gets the logger for this factory and category, creating it once on first use.</summary>
    /// <typeparam name="T">The category type.</typeparam>
    /// <param name="loggerFactory">The application-owned logger factory.</param>
    /// <returns>The category logger owned by this factory.</returns>
    /// <exception cref="ArgumentNullException">The factory is null.</exception>
    public static ILogger<T> CreateBrighterLogger<T>(this ILoggerFactory loggerFactory)
    {
        if (loggerFactory is null)
            throw new ArgumentNullException(nameof(loggerFactory));

        return Cache<T>.Loggers.GetValue(loggerFactory, static factory =>
            new Lazy<ILogger<T>>(() => factory.CreateLogger<T>(), LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private static class Cache<T>
    {
        internal static readonly ConditionalWeakTable<ILoggerFactory, Lazy<ILogger<T>>> Loggers = new();
    }
}
