using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.SharedGenerator;

public class WhenCreatingSharedGeneratorWithLoggerShouldNotThrow : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILogger<Generators.SharedGenerator> _logger;

    public WhenCreatingSharedGeneratorWithLoggerShouldNotThrow()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SharedGeneratorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        
        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.SharedGenerator>();
    }

    [Fact]
    public void When_creating_shared_generator_with_logger_should_not_throw()
    {
        // Arrange & Act
        var generator = new Generators.SharedGenerator(_logger);

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
