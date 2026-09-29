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


namespace Paramore.Brighter.Extensions.Tests;

[System.Obsolete]
public class InertOptInValidationTests
{
    [Test]
    public async Task When_the_opt_in_is_inert_and_throw_on_error_is_true_startup_should_fail()
    {
        // Arrange — a producer-only host (AddBrighter alone), JoinAmbient, all three lifetimes left at
        // the Transient defaults
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddBrighter(options => options.DefaultScopeAffinity = ScopeAffinity.JoinAmbient);
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<BrighterValidationHostedService>().Single();

        // Act & Assert — startup fails with a message naming the affinity, all three lifetimes with their
        // values, that the opt-in has no effect, and the guidance page
        var exception = await Assert.That(() => hostedService.StartAsync(CancellationToken.None)).ThrowsExactly<PipelineValidationException>();

        await Assert.That(exception.Message).Contains("JoinAmbient");
        await Assert.That(exception.Message).Contains("HandlerLifetime");
        await Assert.That(exception.Message).Contains("MapperLifetime");
        await Assert.That(exception.Message).Contains("TransformerLifetime");
        await Assert.That(exception.Message).Contains("Transient");
        await Assert.That(exception.Message).Contains("no effect");
        await Assert.That(exception.Message).Contains("docs/guides/lifetimes-and-scoping.md");
    }

    [Test]
    public async Task When_the_opt_in_is_inert_and_throw_on_error_is_false_the_same_message_should_be_logged_as_error()
    {
        // Arrange — the same inert configuration, but throwOnError: false
        var capturingProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(capturingProvider));
        var builder = services.AddBrighter(options => options.DefaultScopeAffinity = ScopeAffinity.JoinAmbient);
        builder.ValidatePipelines(throwOnError: false);

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<BrighterValidationHostedService>().Single();

        // Act — startup succeeds
        await hostedService.StartAsync(CancellationToken.None);

        // Assert — the identical message is logged at Error instead of thrown
        var errorEntry = await Assert.That(capturingProvider.Entries).HasSingleItem(e => e.Level == LogLevel.Error);
        await Assert.That(errorEntry.Message).Contains("JoinAmbient");
        await Assert.That(errorEntry.Message).Contains("HandlerLifetime");
        await Assert.That(errorEntry.Message).Contains("MapperLifetime");
        await Assert.That(errorEntry.Message).Contains("TransformerLifetime");
        await Assert.That(errorEntry.Message).Contains("Transient");
        await Assert.That(errorEntry.Message).Contains("no effect");
        await Assert.That(errorEntry.Message).Contains("docs/guides/lifetimes-and-scoping.md");
    }
}
