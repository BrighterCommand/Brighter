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
using System.Threading.Tasks;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.AspNetCore
{
    /// <summary>
    /// The ambient <see cref="IAmAScope"/> offered by <see cref="HttpContextScopeProvider"/>: a wrapper
    /// over the current HTTP request's own <see cref="IServiceProvider"/>.
    /// </summary>
    /// <remarks>
    /// ASP.NET Core owns the request's DI scope and disposes it at end of request; Brighter never
    /// created it and must never dispose it, so both disposal members are no-ops (FR-12, C-7).
    /// </remarks>
    public sealed class HttpRequestScope : IAmAServiceProviderScope
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpRequestScope"/> class, capturing the
        /// current request's <see cref="IServiceProvider"/>.
        /// </summary>
        /// <param name="services">The current request's <see cref="IServiceProvider"/>
        /// (<c>HttpContext.RequestServices</c>), captured rather than the <c>HttpContext</c> itself.</param>
        public HttpRequestScope(IServiceProvider services)
        {
            ArgumentNullException.ThrowIfNull(services);
            Services = services;
        }

        /// <inheritdoc />
        public IServiceProvider Services { get; }

        /// <summary>
        /// A no-op: ASP.NET Core owns and disposes the request scope, never Brighter.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// A no-op: ASP.NET Core owns and disposes the request scope, never Brighter.
        /// </summary>
        public ValueTask DisposeAsync() => default;
    }
}
