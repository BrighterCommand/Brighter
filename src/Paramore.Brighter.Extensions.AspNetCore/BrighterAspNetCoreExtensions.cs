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
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.AspNetCore
{
    /// <summary>
    /// Registration extensions offering ASP.NET Core's own request scope as Brighter's ambient scope.
    /// </summary>
    public static class BrighterAspNetCoreExtensions
    {
        /// <summary>
        /// Opts an application into adopting the current HTTP request's own DI scope as Brighter's
        /// ambient scope for an opted-in <c>Scoped</c> pipeline.
        /// </summary>
        /// <remarks>
        /// Reads nothing from <paramref name="services"/> and removes nothing; it never throws on
        /// ordering relative to <c>AddBrighter</c>/<c>AddConsumers</c> and never alters a lifetime.
        /// </remarks>
        /// <param name="services">The service collection to register against.</param>
        /// <param name="affinity">The affinity this opt-in gesture selects. Defaults to
        /// <see cref="ScopeAffinity.JoinAmbient"/>.</param>
        /// <returns>The same <see cref="IServiceCollection"/>, for chaining.</returns>
        public static IServiceCollection AddBrighterRequestScope(
            this IServiceCollection services,
            ScopeAffinity affinity = ScopeAffinity.JoinAmbient)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddHttpContextAccessor();
            services.AddSingleton<IAmAScopeProvider, HttpContextScopeProvider>();
            services.AddSingleton(new ScopeAffinityOverride(affinity));

            return services;
        }
    }
}
