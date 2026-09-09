using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.OutboxGenerator;

public class WhenOutboxConfigurationMissingMessageFactoryShouldUseParentFactory : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILogger<Generators.OutboxGenerator> _logger;

    public WhenOutboxConfigurationMissingMessageFactoryShouldUseParentFactory()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"OutboxGeneratorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.OutboxGenerator>();
    }

    [Fact]
    public async Task When_outbox_configuration_missing_message_factory_should_use_parent_factory()
    {
        // Arrange
        var configuration = new TestConfiguration
        {
            Namespace = "MyApp.Tests",
            DestinationFolder = _testDirectory,
            MessageBuilder = "TestMessageBuilder",
            Outbox = new OutboxConfiguration
            {
                Prefix = "SqlServer",
                Transaction = "SqlTransaction",
                OutboxProvider = "MsSqlOutbox",
            },
        };
        var generator = new Generators.OutboxGenerator(_logger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert - the parent's message builder is what the templates rendered. Asserted through
        // the generated file rather than through the configuration object, because the generator no
        // longer writes its per-render values back onto the caller's configuration. The file is
        // named rather than picked out of an enumeration, whose order the filesystem chooses:
        // IAmAnOutboxProviderSync.cs is the one Sync template that never mentions MessageBuilder.
        var generated = File.ReadAllText(Path.Combine(_testDirectory, "Outbox", "SqlServer",
            "Generated", "Sync", "When_Adding_A_Message_It_Should_Be_Stored_With_All_Properties.cs"));
        Assert.Contains("TestMessageBuilder", generated);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
