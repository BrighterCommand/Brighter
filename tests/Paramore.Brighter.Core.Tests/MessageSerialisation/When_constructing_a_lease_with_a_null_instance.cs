#region Licence
/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

/// <summary>
/// Regression for PR #4254 review finding 5. <see cref="Lease{T}"/> is public API on six public factory and
/// registry interfaces, and callers rely on the invariant "a lease always has an instance" (e.g.
/// <c>TransformerFactory.CreateMessageTransformer</c> checks only <c>lease is null</c> and then dereferences
/// <c>lease.Instance</c>). A third-party factory using the implicit conversion the obvious way — returning
/// <c>_func(t)</c> where <c>_func</c> yields <c>null</c> — would otherwise construct a non-null lease wrapping a
/// null instance, surfacing later as a <c>NullReferenceException</c> instead of a clear configuration error. The
/// constructor must reject a null instance so the invariant is enforced at the one place it can be.
/// </summary>
public class LeaseConstructionTests
{
    [Fact]
    public void When_constructing_a_lease_with_a_null_instance_it_throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new Lease<object>(null!));

        Assert.Equal("instance", exception.ParamName);
    }
}
