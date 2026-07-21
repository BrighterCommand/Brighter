using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;

namespace Paramore.Brighter.Test.Generator.Tests.OutboxGenerator;

public class WhenMultipleOutboxesMissingPrefixShouldUseKeyAsPrefix : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILogger<Generators.OutboxGenerator> _logger;

    public WhenMultipleOutboxesMissingPrefixShouldUseKeyAsPrefix()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"OutboxGeneratorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.OutboxGenerator>();
    }

    [Test]
    public async Task When_multiple_outboxes_missing_prefix_should_use_key_as_prefix()
    {
        // Arrange
        var configuration = new TestConfiguration
        {
            Namespace = "MyApp.Tests",
            DestinationFolder = _testDirectory,
            MessageBuilder = "TestMessageBuilder",
            Outboxes = new Dictionary<string, OutboxConfiguration>
            {
                {
                    "SqlServer",
                    new OutboxConfiguration
                    {
                        OutboxProvider = "MsSqlOutbox",
                        SupportsTransactions = true,
                    }
                },
            },
        };
        var generator = new Generators.OutboxGenerator(_logger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert - prefix should be derived from key
        await Assert.That(configuration.Outboxes["SqlServer"].Prefix).IsEqualTo(".SqlServer");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
