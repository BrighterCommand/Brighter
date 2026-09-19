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

// A scope provider can go on offering a resolution source after the DI scope backing it has already
// been disposed elsewhere in the host - a caller that establishes an ambient and later disposes it
// without ever clearing the provider. Brighter must not let that reach its own resolution as an
// unhandled disposal error: it should notice the source is unusable, fall back to creating and owning
// its own scope exactly as if nothing had been offered, and say so once - not on every Send that hits
// the same stale source, and not confused with either of the other two diagnostics this seam can raise.
public class StaleAmbientDiagnosticTests
{
    [Fact]
    public void When_an_offered_ambient_is_stale_it_should_be_declined_and_reported_once()
    {
        // Arrange - a JoinAmbient host whose registered scope provider will go on offering a resolution
        // source after the scope behind it has already been disposed
        var recorder = new HandlerMarkerRecorder();
        var capturingProvider = new CapturingLoggerProvider();
        var scopeProvider = new AsyncLocalScopeProvider();
        var services = new ServiceCollection();
        services.AddScoped<IMarker, Marker>();
        services.AddSingleton(recorder);
        services.AddSingleton<IAmAScopeProvider>(scopeProvider);
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

        // Arrange - establish an ambient over a scope, then dispose that scope while the provider keeps
        // offering it, the way a caller who forgot to clear the provider on the way out would leave it
        var staleScope = rootProvider.CreateScope();
        scopeProvider.Establish(new AsyncLocalAmbientScope(staleScope.ServiceProvider));
        staleScope.Dispose();

        // Act - two Sends against the same stale ambient
        commandProcessor.Send(new ScopedHandlerCommand());
        commandProcessor.Send(new ScopedHandlerCommand());

        // Assert - neither Send threw, and each resolved and disposed a fresh dependency of its own
        // rather than reaching into the disposed scope
        Assert.Equal(2, recorder.Markers.Count);
        Assert.True(recorder.Markers[0].IsDisposed);
        Assert.True(recorder.Markers[1].IsDisposed);
        Assert.NotSame(recorder.Markers[0], recorder.Markers[1]);

        // Assert - exactly one warning across both Sends, naming the ambient-unusable condition and the
        // provider's own implementation type, and nothing naming either of the other two conditions
        var warnings = capturingProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        var warning = Assert.Single(warnings);
        Assert.Contains("AmbientUnusable", warning.Message);
        Assert.Contains(nameof(AsyncLocalScopeProvider), warning.Message);
        Assert.DoesNotContain(warnings, w => w.Message.Contains("NoAmbientOffered"));
        Assert.DoesNotContain(warnings, w => w.Message.Contains("AmbientIgnoredForAlwaysNew"));
    }
}
