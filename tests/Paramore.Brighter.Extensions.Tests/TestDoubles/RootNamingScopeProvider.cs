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
using Paramore.Brighter.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// An <see cref="IAmAScopeProvider"/> that always offers the container's own root
/// <see cref="IServiceProvider"/> as the ambient's resolution source - constructor-injected, so when
/// registered as a singleton it is handed the same root reference every other Brighter-created
/// singleton factory (including the container-backed pipeline factories) is itself constructed with,
/// not the outer object <c>BuildServiceProvider()</c>/<c>IHost.Services</c> returns to application code.
/// </summary>
public sealed class RootNamingScopeProvider(IServiceProvider services) : IAmAScopeProvider
{
    private readonly IAmAServiceProviderScope _ambient = new AsyncLocalAmbientScope(services);

    /// <inheritdoc />
    public IAmAScope? GetAmbient(ScopeAffinity affinity) => _ambient;
}
