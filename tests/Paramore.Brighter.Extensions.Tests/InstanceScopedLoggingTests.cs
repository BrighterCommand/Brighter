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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// Verifies that Brighter logging is instance-scoped: each <see cref="IAmACommandProcessor"/> logs through the
/// <see cref="ILoggerFactory"/> of the container that built it. This replaces the previous behaviour where the
/// container's factory was copied into the static <c>ApplicationLogging.LoggerFactory</c>, which caused
/// use-after-dispose (when a container was disposed) and cross-talk between two Brighter instances in one process.
/// </summary>
public class InstanceScopedLoggingTests
{
    private static ServiceProvider BuildProvider(CapturingLoggerProvider capture)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(capture);
        });
        services.AddBrighter();
        return services.BuildServiceProvider();
    }

    private class LogProbeEvent() : Event(Guid.NewGuid());

    [Fact]
    public void CommandProcessor_LogsThroughItsOwnContainersFactory_NotAnothers()
    {
        var captureA = new CapturingLoggerProvider();
        var captureB = new CapturingLoggerProvider();

        using var providerA = BuildProvider(captureA);
        using var providerB = BuildProvider(captureB);

        var commandProcessorA = providerA.GetRequiredService<IAmACommandProcessor>();
        providerB.GetRequiredService<IAmACommandProcessor>();

        // Publishing with no subscribers still emits pipeline log lines through A's logger only.
        commandProcessorA.Publish(new LogProbeEvent());

        Assert.NotEmpty(captureA.Entries);
        Assert.Empty(captureB.Entries);
    }

    [Fact]
    public void DisposingOneContainer_DoesNotBreakLoggingInAnother()
    {
        var captureA = new CapturingLoggerProvider();
        var captureB = new CapturingLoggerProvider();

        var providerA = BuildProvider(captureA);
        using var providerB = BuildProvider(captureB);

        // Resolve B first, then A, so that under the previous (buggy) static behaviour the shared
        // ApplicationLogging.LoggerFactory would have ended up pointing at A's factory (last writer wins).
        var commandProcessorB = providerB.GetRequiredService<IAmACommandProcessor>();
        providerA.GetRequiredService<IAmACommandProcessor>();

        // Disposing A disposes A's container (and its ILoggerFactory). B must be entirely unaffected:
        // with instance-scoped logging, B logs through B's own factory and never touches A's.
        providerA.Dispose();

        var exception = Record.Exception(() => commandProcessorB.Publish(new LogProbeEvent()));

        Assert.Null(exception);
        Assert.NotEmpty(captureB.Entries);
        Assert.Empty(captureA.Entries);
    }

    [Fact]
    public void NoLoggerFactoryRegistered_FallsBackSafely_AndDoesNotThrow()
    {
        // No AddLogging(): the DI extension must fall back to a no-op factory, never the disposed/absent one.
        var services = new ServiceCollection();
        services.AddBrighter();
        using var provider = services.BuildServiceProvider();

        var commandProcessor = provider.GetRequiredService<IAmACommandProcessor>();

        var exception = Record.Exception(() => commandProcessor.Publish(new LogProbeEvent()));

        Assert.Null(exception);
    }
}
