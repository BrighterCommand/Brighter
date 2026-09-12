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

using System.Collections.Generic;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// An <see cref="IAmAScopeProvider"/> that records every <see cref="GetAmbient"/> call and the
/// <see cref="ScopeAffinity"/> it carried, so a test can assert exactly which pipelines asked and with
/// what affinity - including the negative case of asking zero times. Carries no reference to
/// <c>Microsoft.Extensions.DependencyInjection</c>, matching the shape a real ambient-source package
/// outside Brighter's own DI package would take.
/// </summary>
public sealed class RecordingScopeProvider : IAmAScopeProvider
{
    private readonly IAmAScope? _ambientToOffer;
    private readonly List<ScopeAffinity> _asks = new();

    /// <summary>
    /// Constructs a provider that records every ask and offers <paramref name="ambientToOffer"/> in
    /// answer to each of them - <see langword="null"/> by default, the ordinary "nothing offered" answer.
    /// </summary>
    public RecordingScopeProvider(IAmAScope? ambientToOffer = null) => _ambientToOffer = ambientToOffer;

    /// <summary>
    /// The affinity carried by every <see cref="GetAmbient"/> call, in the order they arrived.
    /// </summary>
    public IReadOnlyList<ScopeAffinity> Asks
    {
        get { lock (_asks) return _asks.ToArray(); }
    }

    /// <inheritdoc />
    public IAmAScope? GetAmbient(ScopeAffinity affinity)
    {
        lock (_asks) _asks.Add(affinity);
        return _ambientToOffer;
    }
}
