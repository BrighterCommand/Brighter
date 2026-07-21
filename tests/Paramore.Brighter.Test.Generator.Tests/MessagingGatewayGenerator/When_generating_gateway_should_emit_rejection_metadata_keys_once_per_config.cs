using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.MessagingGatewayGenerator;

public class WhenGeneratingGatewayShouldEmitRejectionMetadataKeysOncePerConfig : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public WhenGeneratingGatewayShouldEmitRejectionMetadataKeysOncePerConfig()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"MessagingGatewayGeneratorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    [Fact]
    public async Task When_generating_gateway_should_emit_rejection_metadata_keys_once_per_config()
    {
        // Arrange
        var configuration = new TestConfiguration
        {
            Namespace = "MyApp.Tests",
            DestinationFolder = _testDirectory,
            MessageBuilder = "TestMessageBuilder",
            MessageAssertion = "TestMessageAssertion",
            MessagingGateway = new MessagingGatewayConfiguration
            {
                Prefix = "Test",
                Namespace = "MyApp.Tests",
                MessageGatewayProvider = "TestProvider",
                Publication = "Publication",
                Subscription = "Subscription",
            }
        };
        var generator = new Generators.MessagingGatewayGenerator(_logger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert - exactly one RejectionMetadataKeys.cs is emitted (not one per Reactor/Proactor variant)
        var allEmittedFiles = Directory.GetFiles(_testDirectory, "RejectionMetadataKeys.cs", SearchOption.AllDirectories);
        Assert.Single(allEmittedFiles);

        // Assert - file lives in Generated/ sibling of Reactor/ and Proactor/, not inside either variant
        var expectedPath = Path.Combine(_testDirectory, "MessagingGateway", "Test", "Generated", "RejectionMetadataKeys.cs");
        Assert.True(File.Exists(expectedPath), $"Expected RejectionMetadataKeys.cs at {expectedPath}");

        // Assert - record is sealed and has the five members in the required order
        var content = await File.ReadAllTextAsync(expectedPath);
        Assert.Contains("sealed record RejectionMetadataKeys", content);

        var originalTopicPos = content.IndexOf("string OriginalTopic", StringComparison.Ordinal);
        var originalTypePos = content.IndexOf("string OriginalType", StringComparison.Ordinal);
        var rejectionReasonPos = content.IndexOf("string RejectionReason", StringComparison.Ordinal);
        var rejectionMessagePos = content.IndexOf("string RejectionMessage", StringComparison.Ordinal);
        var rejectionTimestampPos = content.IndexOf("string RejectionTimestamp", StringComparison.Ordinal);

        Assert.True(originalTopicPos >= 0, "OriginalTopic member not found");
        Assert.True(originalTypePos >= 0, "OriginalType member not found");
        Assert.True(rejectionReasonPos >= 0, "RejectionReason member not found");
        Assert.True(rejectionMessagePos >= 0, "RejectionMessage member not found");
        Assert.True(rejectionTimestampPos >= 0, "RejectionTimestamp member not found");

        Assert.True(originalTopicPos < originalTypePos, "OriginalTopic must precede OriginalType");
        Assert.True(originalTypePos < rejectionReasonPos, "OriginalType must precede RejectionReason");
        Assert.True(rejectionReasonPos < rejectionMessagePos, "RejectionReason must precede RejectionMessage");
        Assert.True(rejectionMessagePos < rejectionTimestampPos, "RejectionMessage must precede RejectionTimestamp");

        // Assert - record lives in the parent namespace {{ Namespace }}.MessagingGateway{{ Prefix }}
        Assert.Contains("namespace MyApp.Tests.MessagingGatewayTest;", content);
    }

    [Fact]
    public async Task When_generating_multiple_gateways_should_emit_one_record_per_configuration()
    {
        // Arrange
        var configuration = new TestConfiguration
        {
            Namespace = "MyApp.Tests",
            DestinationFolder = _testDirectory,
            MessageBuilder = "TestMessageBuilder",
            MessageAssertion = "TestMessageAssertion",
            MessagingGateways = new Dictionary<string, MessagingGatewayConfiguration>
            {
                ["Alpha"] = new MessagingGatewayConfiguration
                {
                    MessageGatewayProvider = "AlphaProvider",
                    Publication = "Publication",
                    Subscription = "Subscription",
                },
                ["Beta"] = new MessagingGatewayConfiguration
                {
                    MessageGatewayProvider = "BetaProvider",
                    Publication = "Publication",
                    Subscription = "Subscription",
                }
            }
        };
        var generator = new Generators.MessagingGatewayGenerator(_logger);

        // Act
        await generator.GenerateAsync(configuration);

        // Assert - one record emitted per configuration (N=2), not one per variant
        var allEmittedFiles = Directory.GetFiles(_testDirectory, "RejectionMetadataKeys.cs", SearchOption.AllDirectories);
        Assert.Equal(2, allEmittedFiles.Length);

        // Assert - each record lives in its own prefixed parent namespace
        var alphaPath = Path.Combine(_testDirectory, "MessagingGateway", "Alpha", "Generated", "RejectionMetadataKeys.cs");
        var betaPath = Path.Combine(_testDirectory, "MessagingGateway", "Beta", "Generated", "RejectionMetadataKeys.cs");
        Assert.True(File.Exists(alphaPath), $"Expected Alpha RejectionMetadataKeys.cs at {alphaPath}");
        Assert.True(File.Exists(betaPath), $"Expected Beta RejectionMetadataKeys.cs at {betaPath}");

        var alphaContent = await File.ReadAllTextAsync(alphaPath);
        Assert.Contains("namespace MyApp.Tests.MessagingGateway.Alpha;", alphaContent);

        var betaContent = await File.ReadAllTextAsync(betaPath);
        Assert.Contains("namespace MyApp.Tests.MessagingGateway.Beta;", betaContent);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
