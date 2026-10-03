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
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// An <see cref="IAmAScopeProvider"/> that records the affinity every ask carried and delegates to
/// whichever other <see cref="IAmAScopeProvider"/> is registered in the same container - typically the
/// ASP.NET-backed provider a host's own opt-in already registered (AC-18). Register as
/// <c>AddSingleton&lt;IAmAScopeProvider, DelegatingScopeProviderRecorder&gt;()</c> after that
/// registration, so it wins under last-wins resolution and becomes the one every pipeline actually asks.
/// </summary>
/// <remarks>
/// Cannot take <c>IEnumerable&lt;IAmAScopeProvider&gt;</c> as a constructor dependency: the container
/// would need to finish constructing this instance before it could build the enumerable this instance
/// is itself a member of. Captures the whole <see cref="IServiceProvider"/> instead, and resolves the
/// enumerable - picking the one entry that is not itself - lazily, on the first <see cref="GetAmbient"/>
/// call.
/// </remarks>
public sealed class DelegatingScopeProviderRecorder : IAmAScopeProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<ScopeAffinity> _decisions = new();
    private IAmAScopeProvider? _delegate;

    public DelegatingScopeProviderRecorder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// The affinity carried by each ask made of this provider, in call order.
    /// </summary>
    public IReadOnlyList<ScopeAffinity> Decisions => _decisions;

    /// <inheritdoc />
    public IAmAScope? GetAmbient(ScopeAffinity affinity)
    {
        _decisions.Add(affinity);
        _delegate ??= _serviceProvider
            .GetServices<IAmAScopeProvider>()
            .First(provider => !ReferenceEquals(provider, this));
        return _delegate.GetAmbient(affinity);
    }
}
