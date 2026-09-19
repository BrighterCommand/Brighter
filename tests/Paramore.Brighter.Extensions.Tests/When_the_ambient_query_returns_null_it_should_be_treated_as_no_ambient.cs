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

// A host that opts in to joining an ambient scope, but whose registered scope provider always answers
// with nothing, must behave exactly as if no provider were registered at all - every Send still resolves
// and disposes its own Scoped dependency. The only observable difference is a single warning, raised once
// for the whole container even though more than one Send asks, naming the condition and the provider's
// type. A second, freshly built container of the same shape - same lifetimes, same provider type - but
// left on the default affinity (never opting in to joining an ambient scope) must record no warning at
// all for that same provider type. Without that second host, an implementation that warns on every ask a
// provider fails to answer - opted in or not - would pass this test for the wrong reason.
public class NullAmbientQueryDiagnosticTests
{
    [Fact]
    public void When_the_ambient_query_returns_null_it_should_be_treated_as_no_ambient()
    {
        // Arrange - a JoinAmbient host whose registered scope provider never offers an ambient
        var recorder = new HandlerMarkerRecorder();
        var capturingProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddScoped<IMarker, Marker>();
        services.AddSingleton(recorder);
        services.AddSingleton<IAmAScopeProvider>(new RecordingScopeProvider());
        services.AddLogging(builder => builder.AddProvider(capturingProvider));
        services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
            options.DefaultScopeAffinity = ScopeAffinity.JoinAmbient;
        });
        var commandProcessor = services.BuildServiceProvider().GetRequiredService<IAmACommandProcessor>();

        // Act - two Sends, each asking the provider for an ambient and getting nothing back
        commandProcessor.Send(new ScopedHandlerCommand());
        commandProcessor.Send(new ScopedHandlerCommand());

        // Assert - both Sends succeeded exactly as the unregistered case would: two distinct dependencies,
        // each already disposed by the time its own Send returned
        Assert.Equal(2, recorder.Markers.Count);
        Assert.True(recorder.Markers[0].IsDisposed);
        Assert.True(recorder.Markers[1].IsDisposed);
        Assert.NotSame(recorder.Markers[0], recorder.Markers[1]);

        // Assert - exactly one warning across both Sends, naming the no-ambient-offered condition and the
        // provider's own implementation type
        var warnings = capturingProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        var warning = Assert.Single(warnings);
        Assert.Contains("NoAmbientOffered", warning.Message);
        Assert.Contains(nameof(RecordingScopeProvider), warning.Message);
    }

    [Fact]
    public void When_the_same_provider_type_is_registered_under_AlwaysNew_it_should_not_warn()
    {
        // Arrange - a fresh container of the same shape and the same provider type, but this host never
        // opts in to joining an ambient scope, so its affinity stays the default, AlwaysNew
        var recorder = new HandlerMarkerRecorder();
        var capturingProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddScoped<IMarker, Marker>();
        services.AddSingleton(recorder);
        services.AddSingleton<IAmAScopeProvider>(new RecordingScopeProvider());
        services.AddLogging(builder => builder.AddProvider(capturingProvider));
        services.AddBrighter(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        var commandProcessor = services.BuildServiceProvider().GetRequiredService<IAmACommandProcessor>();

        // Act - two Sends against the same provider type that warned in the JoinAmbient host above
        commandProcessor.Send(new ScopedHandlerCommand());
        commandProcessor.Send(new ScopedHandlerCommand());

        // Assert - no warning at all: an AlwaysNew ask returning nothing is ordinary, not diagnosable
        Assert.Empty(capturingProvider.Entries.Where(e => e.Level == LogLevel.Warning));
    }
}
