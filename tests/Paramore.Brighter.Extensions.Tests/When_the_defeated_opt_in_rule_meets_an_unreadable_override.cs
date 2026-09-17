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
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

// PR #4282 review finding #2 - DefeatedOptIn()'s error factory force-unwraps
// AffinityOverrideRegistrations.Last().ImplementationInstance with '!'. That is safe when the override was
// registered as a constructed instance, but an override registered by factory delegate has no
// ImplementationInstance at all - exactly the shape UnreadableOverride() already detects and reports. If
// such an override is then defeated (the application also registers its own IBrighterOptions ahead of
// AddBrighter), DefeatedOptIn's predicate does not check for the unreadable case, its error factory runs,
// the cast succeeds against null, and reading .Affinity off it throws a NullReferenceException that
// propagates out of hosted-service startup instead of the intended validation message. The fix is for
// DefeatedOptIn to decline to fire when the override is unreadable, since UnreadableOverride() already
// reports that half of the misconfiguration - so this misconfiguration should surface as exactly one
// Warning, not a crash and not a duplicate finding.
public class DefeatedOptInMeetsUnreadableOverrideTests
{
    [Fact]
    public async Task When_a_factory_delegate_override_is_also_defeated_validation_should_not_throw()
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

        // Act - this must not throw a NullReferenceException
        var exception = await Record.ExceptionAsync(() => hostedService.StartAsync(CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task When_a_factory_delegate_override_is_also_defeated_only_the_unreadable_override_warning_should_be_reported()
    {
        // Arrange - same shape as above
        var capturingProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(capturingProvider));
        services.AddSingleton<IBrighterOptions>(new BrighterOptions());
        services.AddSingleton(_ => new ScopeAffinityOverride(ScopeAffinity.JoinAmbient));
        var builder = services.AddBrighter();
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<BrighterValidationHostedService>().Single();

        // Act
        await hostedService.StartAsync(CancellationToken.None);

        // Assert - exactly one finding overall: the unreadable-override Warning, not a second
        // defeated-opt-in finding for the same misconfiguration
        var entry = Assert.Single(capturingProvider.Entries, e => e.Level >= LogLevel.Warning);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("factory delegate", entry.Message);
    }
}
