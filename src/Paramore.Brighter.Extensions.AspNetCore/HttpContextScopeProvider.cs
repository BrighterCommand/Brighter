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

using Microsoft.AspNetCore.Http;

namespace Paramore.Brighter.Extensions.AspNetCore
{
    /// <summary>
    /// Offers the current HTTP request's own DI scope as Brighter's ambient scope.
    /// </summary>
    /// <remarks>
    /// Registered by <see cref="BrighterAspNetCoreExtensions.AddBrighterRequestScope"/>; takes
    /// <see cref="IHttpContextAccessor"/> as a constructor dependency, not a static, so it can be
    /// substituted in a test. No behaviour yet - lands with the T6.3 implementation.
    /// </remarks>
    public sealed class HttpContextScopeProvider : IAmAScopeProvider
    {
        private readonly IHttpContextAccessor _accessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpContextScopeProvider"/> class.
        /// </summary>
        /// <param name="accessor">Gives access to the current request's <see cref="HttpContext"/>.</param>
        public HttpContextScopeProvider(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        /// <inheritdoc />
        public IAmAScope? GetAmbient(ScopeAffinity affinity) => null;
    }
}
