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
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.AspNetCore.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.AspNetCore.Tests;

// AC-19 (FR-18, FR-13, FR-24.2, D19), ADR 0073 step 1 - the opted-in host of T6.3 also carries call sites
// that never run inside an HTTP request: an IHostedService (Send from StartAsync, before any request has
// ever arrived) and a plain background thread (Send with no HttpContext ambient on that thread either).
// HttpContextScopeProvider.GetAmbient already returns null whenever IHttpContextAccessor.HttpContext is
// null, so both calls should behave exactly as if no ambient scope provider were registered at all: each
// resolves and disposes its own fresh Scoped dependency, and neither call throws. Because the host is
// opted in, both asks still carry JoinAmbient, so the "no ambient offered" diagnostic latches once for the
// whole container - across the two calls together, exactly one warning, not two and not zero.
public class NoHttpContextScopeTests
{
    [Fact]
    public async Task When_a_hosted_service_and_a_background_thread_have_no_http_context_they_should_each_get_a_fresh_owned_scope()
    {
        // Arrange - the opted-in host, plus a hosted service that Sends before the host finishes starting,
        // and a capturing logger provider that also sees every Brighter log written through the static
        // ApplicationLogging.LoggerFactory once this host's own CommandProcessor has been built
        var capturingProvider = new CapturingLoggerProvider();
        await using var factory = new PlaceOrderWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<NoHttpContextRecorder>();
                services.AddHostedService<NoHttpContextHostedService>();
                services.AddLogging(logging => logging.AddProvider(capturingProvider));
            });
        });

        // Act - accessing Services starts the host, which runs the hosted service's StartAsync (the first
        // Send, with no HttpContext because no request has ever been made); a plain background thread makes
        // the second Send, again with no HttpContext, since only ASP.NET's own request pipeline ever sets one
        var recorder = factory.Services.GetRequiredService<NoHttpContextRecorder>();
        var commandProcessor = factory.Services.GetRequiredService<IAmACommandProcessor>();

        Exception? backgroundThreadException = null;
        var backgroundThread = new Thread(() =>
        {
            try
            {
                commandProcessor.Send(new NoHttpContextCommand());
            }
            catch (Exception ex)
            {
                backgroundThreadException = ex;
            }
        });
        backgroundThread.Start();
        backgroundThread.Join();

        // Assert - neither call threw, each resolved and disposed its own, distinct Scoped dependency
        Assert.Null(backgroundThreadException);
        Assert.Equal(2, recorder.Markers.Count);
        var markers = recorder.Markers.ToArray();
        Assert.NotSame(markers[0], markers[1]);
        Assert.All(markers, marker => Assert.Equal(1, marker.DisposeCount));

        // Assert - no entry at Error or above from either call
        Assert.DoesNotContain(capturingProvider.Entries, e => e.Level >= LogLevel.Error);

        // Assert - exactly one warning across both calls, naming the no-ambient-offered condition and the
        // ASP.NET provider's own implementation type - the latch is once per (condition, provider type) pair
        // for the whole container, not once per call
        var warnings = capturingProvider.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        var warning = Assert.Single(warnings);
        Assert.Contains("NoAmbientOffered", warning.Message);
        Assert.Contains(nameof(HttpContextScopeProvider), warning.Message);
    }
}
