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
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ConsumerHostInertOptInValidationTests
{
    [Fact]
    public async Task When_a_consumer_host_owns_validation_the_inert_opt_in_error_should_still_fire()
    {
        // Arrange — AddBrighter before AddConsumers, so the producer's BrighterOptions (JoinAmbient, all
        // three lifetimes left at Transient) is the object IBrighterOptions resolves to (C-12); AddConsumers
        // sets ConsumerOwnsValidation, so BrighterValidationHostedService defers and ServiceActivatorHostedService
        // must own validation instead — which AddConsumers alone does not register (C-15, D14)
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddBrighter(options => options.DefaultScopeAffinity = ScopeAffinity.JoinAmbient);
        services.AddConsumers();
        builder.ValidatePipelines(throwOnError: true);
        services.AddHostedService<ServiceActivatorHostedService>();

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<ServiceActivatorHostedService>().Single();

        // Act & Assert — startup still fails, surfaced by ServiceActivatorHostedService rather than
        // BrighterValidationHostedService, carrying the same FR-22.1 message
        var exception = await Assert.ThrowsAsync<PipelineValidationException>(
            () => hostedService.StartAsync(CancellationToken.None));

        Assert.Contains("JoinAmbient", exception.Message);
        Assert.Contains("HandlerLifetime", exception.Message);
        Assert.Contains("MapperLifetime", exception.Message);
        Assert.Contains("TransformerLifetime", exception.Message);
        Assert.Contains("Transient", exception.Message);
        Assert.Contains("no effect", exception.Message);
        Assert.Contains("docs/guides/lifetimes-and-scoping.md", exception.Message);
    }
}
