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
/// A stand-in for an application's own <c>DbContext</c>-shaped dependency (AC-15), registered
/// <c>AddScoped</c> in a test host, so a test can assert a controller and the handler it <c>Send</c>s
/// to resolve the same instance from the request scope.
/// </summary>
public interface IOrderDbContext
{
    /// <summary>
    /// How many times the container has disposed this instance.
    /// </summary>
    int DisposeCount { get; }

    /// <summary>
    /// Throws if this instance has already been disposed, mirroring how a real <c>DbContext</c> behaves
    /// once its scope has torn it down, so a test can prove a caller could still use it.
    /// </summary>
    void EnsureUsable();
}
