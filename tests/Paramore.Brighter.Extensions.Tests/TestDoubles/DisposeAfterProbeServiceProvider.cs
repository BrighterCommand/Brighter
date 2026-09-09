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

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// An ambient's own resolution source that models the race a borrowed ambient creates: the backing
/// <see cref="IServiceScope"/> is still alive when the usability probe asks for a
/// <see cref="ScopedArtefactCache"/> - so the probe passes and the pipeline adopts - but is disposed by
/// its owner immediately afterwards, before any later resolution runs through it. Deterministic: the
/// dispose is triggered by the probe's own first <see cref="ScopedArtefactCache"/> resolution
/// succeeding, not by racing a second thread.
/// </summary>
public sealed class DisposeAfterProbeServiceProvider(IServiceScope backingScope) : IServiceProvider
{
    private bool _disposedAfterProbe;

    /// <inheritdoc />
    public object? GetService(Type serviceType)
    {
        var result = backingScope.ServiceProvider.GetService(serviceType);
        if (!_disposedAfterProbe && serviceType == typeof(ScopedArtefactCache))
        {
            _disposedAfterProbe = true;
            backingScope.Dispose();
        }
        return result;
    }
}
