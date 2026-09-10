using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.SharedGenerator;

public class WhenGeneratingWithCustomMessageFactoryShouldPreserveIt : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILogger<Generators.SharedGenerator> _logger;

    public WhenGeneratingWithCustomMessageFactoryShouldPreserveIt()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SharedGeneratorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.SharedGenerator>();
    }

    [Fact]
    public async Task When_generating_with_custom_message_factory_should_preserve_it()
    {
        // Arrange - through the loader, so that this asserts the defaulting leaves a configured
        // value alone rather than asserting a property nothing writes to
        var configurationFile = Path.Combine(_testDirectory, TestConfigurationLoader.ConfigurationFileName);
        File.WriteAllText(configurationFile, """
            {
              "Namespace": "MyApp.Tests",
              "MessageBuilder": "TestMessageBuilder"
            }
            """);
        var configuration = TestConfigurationLoader.Load(
            configurationFile, defaultDestinationFolder: _testDirectory)!;
        var generator = new Generators.SharedGenerator(_logger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert - the configured builder survives, and only the unset assertion is defaulted
        Assert.Equal("TestMessageBuilder", configuration.MessageBuilder);
        Assert.Equal("DefaultMessageAssertion", configuration.MessageAssertion);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
