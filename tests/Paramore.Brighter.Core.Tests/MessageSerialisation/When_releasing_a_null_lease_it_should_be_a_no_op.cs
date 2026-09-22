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

using System.Threading.Tasks;
using Paramore.Brighter.Core.Tests.MessageSerialisation.Test_Doubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageSerialisation;

/// <summary>
/// Regression for PR #4254 review finding 1. <c>Get</c>/<c>GetAsync</c> return <see cref="Lease{T}"/>?, but the
/// release surface took a non-nullable lease and dereferenced it immediately, so <c>Release(null)</c> — the case
/// a nullable-oblivious netstandard2.0 caller reaches, and the case every call site currently guards against by
/// hand — threw a <see cref="System.NullReferenceException"/>. That contradicts the documented contract that
/// "an over-release of a lease is a harmless no-op". Widening the release parameters to <see cref="Lease{T}"/>?
/// with an early return makes over-release genuinely harmless, for null as much as for a stale token.
/// </summary>
public class MessageMapperRegistryNullReleaseTests
{
    private readonly MessageMapperRegistry _registry = new(
        new SimpleMessageMapperFactory(_ => new MyTransformableCommandMessageMapper()),
        new SimpleMessageMapperFactoryAsync(_ => new MyTransformableCommandMessageMapperAsync()));

    [Fact]
    public void When_releasing_a_null_sync_mapper_lease_it_should_not_throw()
    {
        //act
        var exception = Catch.Exception(() =>
            _registry.Release((Lease<IAmAMessageMapper<MyTransformableCommand>>?)null));

        //assert
        Assert.Null(exception);
    }

    [Fact]
    public void When_releasing_a_null_async_mapper_lease_it_should_not_throw()
    {
        //act
        var exception = Catch.Exception(() =>
            _registry.Release((Lease<IAmAMessageMapperAsync<MyTransformableCommand>>?)null));

        //assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task When_release_async_of_a_null_mapper_lease_it_should_not_throw()
    {
        //act
        var exception = await Catch.ExceptionAsync(async () =>
            await _registry.ReleaseAsync((Lease<IAmAMessageMapperAsync<MyTransformableCommand>>?)null));

        //assert
        Assert.Null(exception);
    }
}
