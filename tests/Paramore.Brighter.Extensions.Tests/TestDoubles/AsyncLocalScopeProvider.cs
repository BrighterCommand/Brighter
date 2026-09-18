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

using System.Threading;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// An <see cref="IAmAScopeProvider"/> that holds its ambient in an <see cref="AsyncLocal{T}"/>, the
/// shape a non-ASP.NET console host would use to flow a unit-of-work scope through a logical call
/// without ever touching <c>HttpContext</c>. Carries no reference to <c>Microsoft.AspNetCore.*</c> or
/// any web hosting package.
/// </summary>
public sealed class AsyncLocalScopeProvider : IAmAScopeProvider
{
    private readonly AsyncLocal<IAmAScope?> _ambient = new();

    /// <summary>
    /// Establishes <paramref name="ambient"/> as the scope every <see cref="GetAmbient"/> call answers
    /// with, for the calling logical flow and anything it awaits.
    /// </summary>
    public void Establish(IAmAScope ambient) => _ambient.Value = ambient;

    /// <summary>
    /// Clears the ambient for the calling logical flow, so a later ask answers <see langword="null"/>.
    /// </summary>
    public void Clear() => _ambient.Value = null;

    /// <inheritdoc />
    public IAmAScope? GetAmbient(ScopeAffinity affinity) => _ambient.Value;
}
