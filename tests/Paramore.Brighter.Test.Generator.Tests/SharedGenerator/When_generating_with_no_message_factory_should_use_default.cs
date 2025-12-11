using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.SharedGenerator;

public class WhenGeneratingWithNoMessageFactoryShouldUseDefault : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILogger<Generators.SharedGenerator> _logger;

    public WhenGeneratingWithNoMessageFactoryShouldUseDefault()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SharedGeneratorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        
        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.SharedGenerator>();
    }

    [Fact]
    public async Task When_generating_with_no_message_factory_should_use_default()
    {
        // Arrange
        var configuration = new TestConfigurationConfiguration
        {
            Namespace = "MyApp.Tests",
            DestinationFolder = _testDirectory
        };
        var generator = new Generators.SharedGenerator(_logger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        Assert.Equal("DefaultMessageFactory", configuration.MessageFactory);
    }
    
    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
