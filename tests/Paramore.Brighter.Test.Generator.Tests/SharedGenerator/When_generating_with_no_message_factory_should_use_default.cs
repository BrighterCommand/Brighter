using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;

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

    [Test]
    public async Task When_generating_with_no_message_factory_should_use_default()
    {
        // Arrange - read through the loader, which is how every real caller gets a configuration.
        // The defaulting used to be a side effect of running this generator first; it belongs to
        // reading the file, so that the generators and the generated-tree audit start from the
        // same root whichever of them runs.
        var configurationFile = Path.Combine(_testDirectory, TestConfigurationLoader.ConfigurationFileName);
        File.WriteAllText(configurationFile, """
            {
              "Namespace": "MyApp.Tests"
            }
            """);
        var configuration = TestConfigurationLoader.Load(
            configurationFile, defaultDestinationFolder: _testDirectory)!;
        var generator = new Generators.SharedGenerator(_logger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert
        await Assert.That(configuration.MessageBuilder).IsEqualTo("DefaultMessageBuilder");
        await Assert.That(configuration.MessageAssertion).IsEqualTo("DefaultMessageAssertion");

        // Assert - and the shared files the generator owns are on disk
        await Assert.That(File.Exists(Path.Combine(_testDirectory, "DefaultMessageBuilder.cs"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(_testDirectory, "DefaultMessageAssertion.cs"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(_testDirectory, "IAmAMessageBuilder.cs"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(_testDirectory, "IAmAMessageAssertion.cs"))).IsTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
