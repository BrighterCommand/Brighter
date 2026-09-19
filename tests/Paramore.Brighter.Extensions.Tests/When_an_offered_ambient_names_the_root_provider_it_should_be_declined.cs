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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

// A scope provider that names a Brighter container's own root IServiceProvider as the ambient's
// resolution source is the most dangerous shape this seam can be handed: without container-level scope
// validation, a Scoped service resolves happily from the root and comes back as one instance shared by
// every caller for the lifetime of the whole process, never disposed by anything - exactly the leak the
// per-pipeline scoping this spec builds exists to prevent. Brighter must recognise its own root and
// refuse to borrow from it, falling back to creating and owning a scope per request exactly as it does
// for any other unusable ambient, and say so once rather than silently defeating its own guarantee.
public class RootProviderAmbientDiagnosticTests
{
    [Fact]
    public void When_an_offered_ambient_names_the_root_provider_it_should_be_declined()
    {
        // Arrange - a JoinAmbient host built without opting into container scope validation, so an
        // implementation that failed to recognise its own root would succeed at resolving from it
        // instead of throwing. The registered scope provider is constructor-injected with IServiceProvider,
        // so it always offers the very root reference Brighter's own factories are themselves constructed
        // with - not the outer object BuildServiceProvider() hands back to application code, which is a
        // different wrapper around the same container and is not what this rule targets.
        var recorder = new HandlerMarkerRecorder();
        var capturingProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddScoped<IMarker, Marker>();
        services.AddSingleton(recorder);
        services.AddSingleton<IAmAScopeProvider, RootNamingScopeProvider>();
        services.AddLogging(builder => builder.AddProvider(capturingProvider));
        services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
            options.DefaultScopeAffinity = ScopeAffinity.JoinAmbient;
        });
        var rootProvider = services.BuildServiceProvider();
        var commandProcessor = rootProvider.GetRequiredService<IAmACommandProcessor>();

        // Act - two Sends, two separate requests, both against the same root-naming ambient
        commandProcessor.Send(new ScopedHandlerCommand());
        commandProcessor.Send(new ScopedHandlerCommand());

        // Assert - each Send resolved and disposed a scope Brighter created and owns: two distinct
        // instances, each already disposed by the time its own Send returned, neither shared with the
        // other
        Assert.Equal(2, recorder.Markers.Count);
        Assert.True(recorder.Markers[0].IsDisposed);
        Assert.True(recorder.Markers[1].IsDisposed);
        Assert.NotSame(recorder.Markers[0], recorder.Markers[1]);

        // Assert - exactly one warning across both Sends, naming the ambient-unusable condition and the
        // provider's own implementation type, and nothing naming either of the other two conditions
        var warnings = capturingProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        var warning = Assert.Single(warnings);
        Assert.Contains("AmbientUnusable", warning.Message);
        Assert.Contains(nameof(RootNamingScopeProvider), warning.Message);
        Assert.DoesNotContain(warnings, w => w.Message.Contains("NoAmbientOffered"));
        Assert.DoesNotContain(warnings, w => w.Message.Contains("AmbientIgnoredForAlwaysNew"));
    }
}
