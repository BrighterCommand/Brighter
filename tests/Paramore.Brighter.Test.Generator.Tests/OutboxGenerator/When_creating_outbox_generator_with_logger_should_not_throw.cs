using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.OutboxGenerator;

public class WhenCreatingOutboxGeneratorWithLoggerShouldNotThrow : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILogger<Generators.OutboxGenerator> _logger;

    public WhenCreatingOutboxGeneratorWithLoggerShouldNotThrow()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"OutboxGeneratorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.OutboxGenerator>();
    }

    [Fact]
    public void When_creating_outbox_generator_with_logger_should_not_throw()
    {
        // Arrange & Act
        var generator = new Generators.OutboxGenerator(_logger);

        // Assert
        Assert.NotNull(generator);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
