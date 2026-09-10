using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

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

    [Fact]
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

        // Assert - the key became both the destination folder and, dot-qualified, the namespace
        // suffix. Asserted through the generated file rather than through the configuration object,
        // because the generator no longer writes its per-render values back onto the caller's
        // configuration. The file is named rather than picked out of an enumeration, whose order
        // the filesystem chooses.
        var syncFolder = Path.Combine(_testDirectory, "Outbox", "SqlServer", "Generated", "Sync");
        Assert.True(Directory.Exists(syncFolder), $"Expected the key to name the folder: {syncFolder}");

        var generated = File.ReadAllText(Path.Combine(syncFolder,
            "When_Adding_A_Message_It_Should_Be_Stored_With_All_Properties.cs"));
        Assert.Contains(".SqlServer.Sync;", generated);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
