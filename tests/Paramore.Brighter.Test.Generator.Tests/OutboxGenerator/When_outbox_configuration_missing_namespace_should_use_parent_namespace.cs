using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.OutboxGenerator;

public class WhenOutboxConfigurationMissingNamespaceShouldUseParentNamespace : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILogger<Generators.OutboxGenerator> _logger;

    public WhenOutboxConfigurationMissingNamespaceShouldUseParentNamespace()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"OutboxGeneratorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.OutboxGenerator>();
    }

    [Fact]
    public async Task When_outbox_configuration_missing_namespace_should_use_parent_namespace()
    {
        // Arrange
        var configuration = new TestConfiguration
        {
            Namespace = "MyApp.Tests",
            DestinationFolder = _testDirectory,
            MessageFactory = "TestMessageFactory",
            Outbox = new OutboxConfiguration
            {
                Prefix = "SqlServer",
                Transaction = "SqlTransaction",
                OutboxProvider = "MsSqlOutbox"
            }
        };
        var generator = new Generators.OutboxGenerator(_logger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert - namespace should be inherited from parent configuration
        Assert.Equal("MyApp.Tests", configuration.Outbox.Namespace);
    }
    
    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
