#region Licence
/* The MIT License (MIT)
Copyright © 2026 Tom Longhurst

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
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class PipelineLoggingReuseTests
{
    [Fact]
    public async Task When_sending_repeatedly_should_reuse_loggers_and_configure_sync_and_async_decorators()
    {
        // Arrange
        using var factory = new CountingLoggerFactory();
        var capture = new InstanceScopedCapturingLoggerProvider();
        factory.AddProvider(capture);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILoggerFactory>(factory);
        // AddBrighter discovers these handlers in the loaded Paramore.Brighter test assembly.
        services.AddBrighter();
        using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        processor.Send(new LoggingReviewCommand());
        await processor.SendAsync(new LoggingReviewAsyncCommand());
        var initialCreations = factory.Creations.Values.Sum();
        capture.Clear();

        // Act
        processor.Send(new LoggingReviewCommand());
        await processor.SendAsync(new LoggingReviewAsyncCommand());

        // Assert
        Assert.Equal(initialCreations, factory.Creations.Values.Sum());
        Assert.Contains(capture.Entries, entry => entry.StartsWith("Paramore.Brighter.RequestHandler:") && entry.Contains("Passing request"));
        Assert.Contains(capture.Entries, entry => entry.StartsWith("Paramore.Brighter.RequestHandlerAsync:") && entry.Contains("Passing request"));
    }

    [Fact]
    public async Task When_a_manual_async_handler_chain_is_configured_should_forward_the_request()
    {
        // Arrange
        var first = new ConsumerGlobalInboxAsyncCommandHandler();
        var second = new ConsumerGlobalInboxAsyncCommandHandler();
        first.ConfigureLogging(NullLoggerFactory.Instance);
        second.ConfigureLogging(NullLoggerFactory.Instance);
        first.SetSuccessor(second);
        var command = new ConsumerGlobalInboxAsyncCommand();
        // Act
        await first.HandleAsync(command);
        // Assert
        Assert.Equal(2, command.HandleCount);
    }
}
