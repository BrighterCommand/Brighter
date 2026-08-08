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
/// Regression for PR #4254 review finding 1. A token-less lease releases nothing — that is correct for a
/// factory that holds no per-resolution state (a shared instance, or a no-op factory), but it must be a
/// <b>named, deliberate</b> act, never something a bare instance silently becomes. On master an implicit
/// conversion from <c>T</c> to <see cref="Lease{T}"/> meant a third-party factory writing the mechanical
/// migration <c>return mapper;</c> compiled to a token-less lease, making every <c>Release</c> a silent no-op
/// and reintroducing the very leak (#4252) this change closes. The conversion is replaced by
/// <see cref="Lease{T}.Untracked"/> so the reader can see nothing is reclaimed, and a factory that opens a
/// per-resolution scope must pass its release token via the constructor.
/// </summary>
public class UntrackedLeaseTests
{
    [Fact]
    public void When_creating_an_untracked_lease_it_holds_the_instance_with_no_release_token()
    {
        var instance = new object();

        var lease = Lease<object>.Untracked(instance);

        Assert.Same(instance, lease.Instance);
        Assert.Null(lease.ReleaseToken);
    }

    [Fact]
    public void When_creating_an_untracked_lease_for_a_null_instance_it_throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => Lease<object>.Untracked(null!));

        Assert.Equal("instance", exception.ParamName);
    }
}
