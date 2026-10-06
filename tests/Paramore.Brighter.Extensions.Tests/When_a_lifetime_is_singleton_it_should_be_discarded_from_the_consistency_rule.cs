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

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class SingletonLifetimeDiscardedFromConsistencyRuleTests
{
    [Theory]
    [InlineData(ServiceLifetime.Scoped, ServiceLifetime.Singleton, ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Singleton, ServiceLifetime.Singleton, ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Transient, ServiceLifetime.Singleton, ServiceLifetime.Transient)]
    [InlineData(ServiceLifetime.Transient, ServiceLifetime.Transient, ServiceLifetime.Transient)]
    public async Task When_a_singleton_lifetime_is_present_the_remainder_should_not_be_compared_against_it(
        ServiceLifetime handlerLifetime, ServiceLifetime mapperLifetime, ServiceLifetime transformerLifetime)
    {
        // Arrange — a producer-only host whose triple leaves a uniform (or empty) remainder once
        // Singleton is discarded — the consistency rule must not fire against any of these four.
        // AlwaysNew keeps the unrelated FR-22.1 inert-opt-in rule out of play (it only ever fires
        // under JoinAmbient), isolating this fact to the consistency rule alone.
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddBrighter(options =>
        {
            options.DefaultScopeAffinity = ScopeAffinity.AlwaysNew;
            options.HandlerLifetime = handlerLifetime;
            options.MapperLifetime = mapperLifetime;
            options.TransformerLifetime = transformerLifetime;
        });
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<BrighterValidationHostedService>().Single();

        // Act & Assert — startup succeeds, the consistency rule raises nothing
        await hostedService.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task When_discarding_singleton_leaves_a_mixed_remainder_startup_should_fail()
    {
        // Arrange — Mapper is Singleton and is discarded, leaving {Scoped, Transient} — still mixed.
        // AlwaysNew, matching the four negative facts above and T7.3's own confirmation that the
        // consistency rule is not conditional on affinity.
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddBrighter(options =>
        {
            options.DefaultScopeAffinity = ScopeAffinity.AlwaysNew;
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Singleton;
            options.TransformerLifetime = ServiceLifetime.Transient;
        });
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<BrighterValidationHostedService>().Single();

        // Act & Assert — discarding Singleton still leaves a mixed remainder, so the consistency error fires
        var exception = await Assert.ThrowsAsync<PipelineValidationException>(
            () => hostedService.StartAsync(CancellationToken.None));

        Assert.Contains("HandlerLifetime", exception.Message);
        Assert.Contains("MapperLifetime", exception.Message);
        Assert.Contains("TransformerLifetime", exception.Message);
        Assert.Contains("Scoped", exception.Message);
        Assert.Contains("Transient", exception.Message);
        Assert.Contains("do not share", exception.Message);
        Assert.Contains("docs/guides/lifetimes-and-scoping.md", exception.Message);
    }
}
