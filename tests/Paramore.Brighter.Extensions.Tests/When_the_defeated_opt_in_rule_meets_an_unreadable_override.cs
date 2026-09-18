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

// PR #4282 review finding #2 - DefeatedOptIn()'s error factory force-unwraps
// AffinityOverrideRegistrations.Last().ImplementationInstance with '!'. That is safe when the override was
// registered as a constructed instance, but an override registered by factory delegate has no
// ImplementationInstance at all - exactly the shape UnreadableOverride() already detects and reports. If
// such an override is then defeated (the application also registers its own IBrighterOptions ahead of
// AddBrighter), DefeatedOptIn's predicate must not force-unwrap a null instance and throw a
// NullReferenceException - but it must still report the defeat, with a value-free message, rather than
// declining to fire. Declining would silently drop the FR-22.4 Error for a defeat that genuinely happened,
// so with throwOnError:true an application whose opt-in was defeated would start up successfully instead
// of failing validation. The unreadable-override Warning and the defeated-opt-in Error are not duplicates
// of each other - they report two different problems (the override's value can't be read; the opt-in
// never took effect) that happen to co-occur here.
public class DefeatedOptInMeetsUnreadableOverrideTests
{
    [Fact]
    public async Task When_a_factory_delegate_override_is_also_defeated_validation_should_not_throw_a_null_reference_exception()
    {
        // Arrange - the application registers its own IBrighterOptions ahead of AddBrighter (defeating the
        // opt-in, D18), and separately registers a ScopeAffinityOverride by factory delegate rather than as
        // a constructed instance (the shape DefeatedOptIn's error factory cannot safely force-unwrap)
        var capturingProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(capturingProvider));
        services.AddSingleton<IBrighterOptions>(new BrighterOptions());
        services.AddSingleton(_ => new ScopeAffinityOverride(ScopeAffinity.JoinAmbient));
        var builder = services.AddBrighter();
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<BrighterValidationHostedService>().Single();

        // Act - the defeat is real, so throwOnError:true is expected to fail startup - just not with a
        // NullReferenceException from the unreadable override
        var exception = await Record.ExceptionAsync(() => hostedService.StartAsync(CancellationToken.None));

        // Assert
        Assert.IsNotType<NullReferenceException>(exception);
        Assert.IsType<PipelineValidationException>(exception);
    }

    [Fact]
    public async Task When_a_factory_delegate_override_is_also_defeated_both_the_unreadable_override_warning_and_the_defeated_opt_in_error_should_be_reported()
    {
        // Arrange - same shape as above, but throwOnError:false so both findings are logged rather than
        // one of them being raised as an exception
        var capturingProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(capturingProvider));
        services.AddSingleton<IBrighterOptions>(new BrighterOptions());
        services.AddSingleton(_ => new ScopeAffinityOverride(ScopeAffinity.JoinAmbient));
        var builder = services.AddBrighter();
        builder.ValidatePipelines(throwOnError: false);

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<BrighterValidationHostedService>().Single();

        // Act
        await hostedService.StartAsync(CancellationToken.None);

        // Assert - the unreadable-override Warning, naming the factory-delegate shape
        var warningEntry = Assert.Single(capturingProvider.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("factory delegate", warningEntry.Message);

        // Assert - the defeated-opt-in Error, reported without naming an affinity value it cannot read
        var errorEntry = Assert.Single(capturingProvider.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("supplied by the application", errorEntry.Message);
        Assert.DoesNotContain(nameof(ScopeAffinity.JoinAmbient), errorEntry.Message);
        Assert.DoesNotContain(nameof(ScopeAffinity.AlwaysNew), errorEntry.Message);
    }
}
