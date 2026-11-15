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

namespace Paramore.Brighter.Extensions.Tests;

[NotInParallel(LoggerCaptureCollection.NAME)]
public class LateLoggingFactoryTests : IDisposable
{
    private readonly ILoggerFactory _originalFactory = ApplicationLogging.LoggerFactory;
    private readonly ILoggerFactory _initialFactory = new LoggerFactory();

    public LateLoggingFactoryTests()
    {
        ApplicationLogging.LoggerFactory = _initialFactory;
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_a_logger_precedes_command_processor_resolution_should_use_the_host_logging_factory(bool useRuntimeType)
    {
        // Arrange
        var logger = useRuntimeType
            ? ApplicationLogging.CreateLogger(typeof(LateLoggingFactoryTests))
            : ApplicationLogging.CreateLogger<LateLoggingFactoryTests>();
        logger.LogWarning("Before host initialization");
        await Assert.That(logger.IsEnabled(LogLevel.Warning)).IsFalse();
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
        await Assert.That(logger.IsEnabled(LogLevel.Warning)).IsTrue();
        var entry = await Assert.That(System.Linq.Enumerable.Where(capture.Entries, e => e.EventId == eventId)).HasSingleItem();
        await Assert.That(entry.CategoryName).IsEqualTo(typeof(LateLoggingFactoryTests).FullName);
        await Assert.That(entry.Level).IsEqualTo(LogLevel.Warning);
        await Assert.That(entry.Message).IsEqualTo("Message after-initialization failed");
        await Assert.That(entry.Exception).IsSameReferenceAs(exception);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_the_logging_factory_is_replaced_should_route_existing_loggers_with_the_current_filters(bool useRuntimeType)
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
        await Assert.That(logger.IsEnabled(LogLevel.Information)).IsTrue();
        var secondCapture = new CapturingLoggerProvider();
        using var secondFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Error).AddProvider(secondCapture));

        // Act
        ApplicationLogging.LoggerFactory = secondFactory;
        logger.LogInformation("Filtered by the second factory");
        logger.LogError("Second factory");

        // Assert
        await Assert.That(logger.IsEnabled(LogLevel.Information)).IsFalse();
        await Assert.That(logger.IsEnabled(LogLevel.Error)).IsTrue();
        await Assert.That((await Assert.That(firstCapture.Entries).HasSingleItem()).Message).IsEqualTo("First factory");
        await Assert.That((await Assert.That(secondCapture.Entries).HasSingleItem()).Message).IsEqualTo("Second factory");
    }

    [Test]
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
        var entry = await Assert.That(System.Linq.Enumerable.Where(capture.Entries, e => e.EventId.Name == "ConfirmationCallbackFault")).HasSingleItem();
        await Assert.That(entry.CategoryName).IsEqualTo(typeof(InMemoryMessageProducer).FullName);
        await Assert.That(entry.Level).IsEqualTo(LogLevel.Warning);
        await Assert.That(entry.Message).Contains("after-initialization");
        await Assert.That(entry.Exception).IsTypeOf<InvalidOperationException>();
        await Assert.That(entry.Exception.Message).IsEqualTo("Confirmation failed");
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
