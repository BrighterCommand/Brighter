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

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.BoxProvisioning.Tests.TestDoubles;

/// <summary>
/// Configurable <see cref="IAmABoxProvisioner"/> test double. Honours either a happy-path
/// completion or a forced exception so tests can drive the hosted service through both
/// success and failure log paths.
/// </summary>
internal sealed class StubBoxProvisioner : IAmABoxProvisioner
{
    private readonly Exception? _throwOnProvision;

    public StubBoxProvisioner(BoxType boxType, string boxTableName, Exception? throwOnProvision = null)
    {
        BoxType = boxType;
        BoxTableName = boxTableName;
        _throwOnProvision = throwOnProvision;
    }

    public BoxType BoxType { get; }
    public string BoxTableName { get; }

    public Task ProvisionAsync(CancellationToken cancellationToken = default)
    {
        if (_throwOnProvision is not null) throw _throwOnProvision;
        return Task.CompletedTask;
    }
}
