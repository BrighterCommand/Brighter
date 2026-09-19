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
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class MixedTransientAndScopedValidationTests
{
    [Fact]
    public async Task When_transient_and_scoped_are_mixed_and_affinity_is_join_ambient_startup_should_fail()
    {
        // Arrange — a producer-only host with a mixed triple: Handler and Transformer Scoped, Mapper Transient
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddBrighter(options =>
        {
            options.DefaultScopeAffinity = ScopeAffinity.JoinAmbient;
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Transient;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<BrighterValidationHostedService>().Single();

        // Act & Assert — startup fails, the error is not conditional on affinity
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

    [Fact]
    public async Task When_transient_and_scoped_are_mixed_and_affinity_is_always_new_startup_should_fail()
    {
        // Arrange — the identical mixed triple, but with affinity AlwaysNew — the error is not about
        // adopting an ambient scope, it is that the mixed pair can never share a pipeline-scoped dependency
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddBrighter(options =>
        {
            options.DefaultScopeAffinity = ScopeAffinity.AlwaysNew;
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Transient;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<BrighterValidationHostedService>().Single();

        // Act & Assert — startup fails identically under AlwaysNew
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

    [Fact]
    public async Task When_transient_and_scoped_are_mixed_and_throw_on_error_is_false_the_same_message_should_be_logged_as_error()
    {
        // Arrange — the same mixed triple, but throwOnError: false
        var capturingProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(capturingProvider));
        var builder = services.AddBrighter(options =>
        {
            options.DefaultScopeAffinity = ScopeAffinity.JoinAmbient;
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Transient;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        builder.ValidatePipelines(throwOnError: false);

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<BrighterValidationHostedService>().Single();

        // Act — startup succeeds
        await hostedService.StartAsync(CancellationToken.None);

        // Assert — the identical message is logged at Error instead of thrown
        var errorEntry = Assert.Single(capturingProvider.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("HandlerLifetime", errorEntry.Message);
        Assert.Contains("MapperLifetime", errorEntry.Message);
        Assert.Contains("TransformerLifetime", errorEntry.Message);
        Assert.Contains("Scoped", errorEntry.Message);
        Assert.Contains("Transient", errorEntry.Message);
        Assert.Contains("do not share", errorEntry.Message);
        Assert.Contains("docs/guides/lifetimes-and-scoping.md", errorEntry.Message);
    }
}
