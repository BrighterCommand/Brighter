using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.OutboxGenerator;

public class WhenMultipleOutboxesMissingPrefixShouldUseKeyAsPrefix: IDisposable
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
        var configuration = new TestConfigurationConfiguration
        {
            Namespace = "MyApp.Tests",
            DestinationFolder = _testDirectory,
            MessageFactory = "TestMessageFactory",
            Outboxes = new Dictionary<string, OutboxConfiguration>
            {
                { 
                    "SqlServer", 
                    new OutboxConfiguration 
                    { 
                        OutboxProvider = "MsSqlOutbox",
                        HasSupportToTransaction = true
                    } 
                }
            }
        };
        var generator = new Generators.OutboxGenerator(_logger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert - prefix should be derived from key
        Assert.Equal(".SqlServer", configuration.Outboxes["SqlServer"].Prefix);
    }
    
    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
