using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.OutboxGenerator;

public class WhenGeneratingWithMultipleOutboxesShouldCompleteWithoutError : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILogger<Generators.OutboxGenerator> _logger;

    public WhenGeneratingWithMultipleOutboxesShouldCompleteWithoutError()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"OutboxGeneratorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        
        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.OutboxGenerator>();
    }

    [Fact]
    public async Task When_generating_with_single_outbox_should_complete_without_error()
    {
        // Arrange
        var configuration = new TestConfigurationConfiguration
        {
            Namespace = "MyApp.Tests",
            DestinationFolder = _testDirectory,
            MessageFactory = "TestMessageFactory",
            Outbox = new OutboxConfiguration
            {
                Prefix = "SqlServer",
                Transaction = "SqlTransaction",
                OutboxProvider = "MsSqlOutbox",
                HasSupportToTransaction = true
            }
        };
        var generator = new Generators.OutboxGenerator(_logger);

        // Act & Assert - should not throw
        await generator.GenerateAsync(configuration);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
