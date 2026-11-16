#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Logging;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

[Collection(LoggerCaptureCollection.NAME)]
public class LateLoggingFactoryTests : IDisposable
{
    private readonly ILoggerFactory _originalFactory = ApplicationLogging.LoggerFactory;
    private readonly ILoggerFactory _initialFactory = new LoggerFactory();

    public LateLoggingFactoryTests()
    {
        ApplicationLogging.LoggerFactory = _initialFactory;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_a_logger_precedes_command_processor_resolution_should_use_the_host_logging_factory(bool useRuntimeType)
    {
        // Arrange
        var logger = useRuntimeType
            ? ApplicationLogging.CreateLogger(typeof(LateLoggingFactoryTests))
            : ApplicationLogging.CreateLogger<LateLoggingFactoryTests>();
        logger.LogWarning("Before host initialization");
        Assert.False(logger.IsEnabled(LogLevel.Warning));
        var capture = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(capture));
        services.AddBrighter();
        using var provider = services.BuildServiceProvider();
        var exception = new InvalidOperationException("Publish failed");
        var eventId = new EventId(4690, "LateLogging");

        // Act
        provider.GetRequiredService<IAmACommandProcessor>();
        logger.LogWarning(eventId, exception, "Message {MessageId} failed", "after-initialization");

        // Assert
        Assert.True(logger.IsEnabled(LogLevel.Warning));
        var entry = Assert.Single(capture.Entries, e => e.EventId == eventId);
        Assert.Equal(typeof(LateLoggingFactoryTests).FullName, entry.CategoryName);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Message after-initialization failed", entry.Message);
        Assert.Same(exception, entry.Exception);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_the_logging_factory_is_replaced_should_route_existing_loggers_with_the_current_filters(bool useRuntimeType)
    {
        // Arrange
        var firstCapture = new CapturingLoggerProvider();
        using var firstFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Information).AddProvider(firstCapture));
        ApplicationLogging.LoggerFactory = firstFactory;
        var logger = useRuntimeType
            ? ApplicationLogging.CreateLogger(typeof(LateLoggingFactoryTests))
            : ApplicationLogging.CreateLogger<LateLoggingFactoryTests>();
        logger.LogInformation("First factory");
        Assert.True(logger.IsEnabled(LogLevel.Information));
        var secondCapture = new CapturingLoggerProvider();
        using var secondFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Error).AddProvider(secondCapture));

        // Act
        ApplicationLogging.LoggerFactory = secondFactory;
        logger.LogInformation("Filtered by the second factory");
        logger.LogError("Second factory");

        // Assert
        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Error));
        Assert.Equal("First factory", Assert.Single(firstCapture.Entries).Message);
        Assert.Equal("Second factory", Assert.Single(secondCapture.Entries).Message);
    }

    [Fact]
    public async Task When_a_producer_logs_before_command_processor_resolution_should_log_subsequent_failures_to_the_host()
    {
        // Arrange
        await PublishWithFailingConfirmationAsync("before-initialization");
        var capture = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(capture));
        services.AddBrighter();
        using var provider = services.BuildServiceProvider();

        // Act
        provider.GetRequiredService<IAmACommandProcessor>();
        await PublishWithFailingConfirmationAsync("after-initialization");

        // Assert
        var entry = Assert.Single(capture.Entries, e => e.EventId.Name == "ConfirmationCallbackFault");
        Assert.Equal(typeof(InMemoryMessageProducer).FullName, entry.CategoryName);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("after-initialization", entry.Message);
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Equal("Confirmation failed", entry.Exception.Message);
    }

    public void Dispose()
    {
        ApplicationLogging.LoggerFactory = _originalFactory;
        _initialFactory.Dispose();
    }

    private static async Task PublishWithFailingConfirmationAsync(string messageId)
    {
        await using var producer = new InMemoryMessageProducer(new InternalBus())
        {
            UseAsyncPublishConfirmation = true
        };
        producer.OnMessagePublished += _ => throw new InvalidOperationException("Confirmation failed");
        await producer.SendAsync(new Message(
            new MessageHeader(new Id(messageId), new RoutingKey("late-logging"), MessageType.MT_EVENT),
            new MessageBody("test")));
    }
}
