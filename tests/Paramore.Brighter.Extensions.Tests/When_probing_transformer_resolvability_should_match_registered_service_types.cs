#region Licence
/* The MIT License (MIT)
Copyright © 2026 Miguel Ramirez <xbizzybone@gmail.com>

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
using Paramore.Brighter.Transforms.Transformers;
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.Tests;

public class TransformerResolvabilityProbeTests
{
    [Test]
    public async Task When_probing_transformer_resolvability_should_match_registered_service_types()
    {
        // Arrange — a service collection with one transformer registered; a second transformer
        // type whose constructor throws is also registered to prove the probe never instantiates.
        var services = new ServiceCollection();
        services.AddTransient(typeof(CompressPayloadTransformer));
        services.AddTransient(typeof(ThrowOnConstructTransformer));

        var probe = new ServiceCollectionTransformerResolvabilityProbe(services);

        // Act + Assert — a registered transformer resolves
        await Assert.That(probe.Resolves(typeof(CompressPayloadTransformer))).IsTrue();

        // an unregistered transformer does not resolve
        await Assert.That(probe.Resolves(typeof(UnregisteredTransformer))).IsFalse();

        // and resolvability is answered from registration membership only — the probe does NOT
        // construct the transformer, so a registered type with a throwing constructor still resolves
        await Assert.That(probe.Resolves(typeof(ThrowOnConstructTransformer))).IsTrue();
    }

    [Test]
    public async Task When_probing_an_empty_service_collection_should_not_resolve_any_transformer()
    {
        // Arrange — no transformers registered
        var probe = new ServiceCollectionTransformerResolvabilityProbe(new ServiceCollection());

        // Act + Assert — nothing resolves
        await Assert.That(probe.Resolves(typeof(CompressPayloadTransformer))).IsFalse();
    }

    [Test]
    public void When_constructing_a_probe_with_null_services_should_throw()
    {
        // Act + Assert — a null service collection is a configuration error
        Assert.Throws<ArgumentNullException>(() => new ServiceCollectionTransformerResolvabilityProbe(null!));
    }

    private class UnregisteredTransformer
    {
    }

    private class ThrowOnConstructTransformer
    {
        public ThrowOnConstructTransformer()
            => throw new InvalidOperationException("The probe must not instantiate the transformer");
    }
}