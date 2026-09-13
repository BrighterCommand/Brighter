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
using Paramore.Brighter.Extensions.AspNetCore;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;

/// <summary>
/// A hand-rolled <see cref="IAmAScopeProvider"/> that ignores <see cref="ScopeAffinity"/> instead of
/// honouring FR-10, offering (or withholding) the current request's own ambient independently for each
/// affinity - so a test can prove Brighter itself ignores an ambient handed over for an
/// <see cref="ScopeAffinity.AlwaysNew"/> ask, and warns once about it.
/// </summary>
public sealed class AffinityIgnoringScopeProvider : IAmAScopeProvider
{
    private readonly IHttpContextAccessor _accessor;
    private readonly bool _offerForAlwaysNew;
    private readonly bool _offerForJoinAmbient;

    /// <param name="accessor">Gives access to the current request's <see cref="HttpContext"/>.</param>
    /// <param name="offerForAlwaysNew">Whether to offer the current request's ambient for an
    /// <see cref="ScopeAffinity.AlwaysNew"/> ask.</param>
    /// <param name="offerForJoinAmbient">Whether to offer the current request's ambient for a
    /// <see cref="ScopeAffinity.JoinAmbient"/> ask.</param>
    public AffinityIgnoringScopeProvider(IHttpContextAccessor accessor, bool offerForAlwaysNew, bool offerForJoinAmbient)
    {
        _accessor = accessor;
        _offerForAlwaysNew = offerForAlwaysNew;
        _offerForJoinAmbient = offerForJoinAmbient;
    }

    /// <inheritdoc />
    public IAmAScope? GetAmbient(ScopeAffinity affinity)
    {
        var shouldOffer = affinity == ScopeAffinity.AlwaysNew ? _offerForAlwaysNew : _offerForJoinAmbient;
        if (!shouldOffer) return null;

        var services = _accessor.HttpContext?.RequestServices;
        return services is null ? null : new HttpRequestScope(services);
    }
}
