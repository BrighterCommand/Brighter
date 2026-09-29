using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;


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

    [Test]
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
        await Assert.That(allEmittedFiles).HasSingleItem();

        // Assert - file lives in Generated/ sibling of Reactor/ and Proactor/, not inside either variant
        var expectedPath = Path.Combine(_testDirectory, "MessagingGateway", "Test", "Generated", "RejectionMetadataKeys.cs");
        await Assert.That(File.Exists(expectedPath)).IsTrue().Because($"Expected RejectionMetadataKeys.cs at {expectedPath}");

        // Assert - record is sealed and has the five members in the required order
        var content = await File.ReadAllTextAsync(expectedPath);
        await Assert.That(content).Contains("sealed record RejectionMetadataKeys");

        var originalTopicPos = content.IndexOf("string OriginalTopic", StringComparison.Ordinal);
        var originalTypePos = content.IndexOf("string OriginalType", StringComparison.Ordinal);
        var rejectionReasonPos = content.IndexOf("string RejectionReason", StringComparison.Ordinal);
        var rejectionMessagePos = content.IndexOf("string RejectionMessage", StringComparison.Ordinal);
        var rejectionTimestampPos = content.IndexOf("string RejectionTimestamp", StringComparison.Ordinal);

        await Assert.That(originalTopicPos >= 0).IsTrue().Because("OriginalTopic member not found");
        await Assert.That(originalTypePos >= 0).IsTrue().Because("OriginalType member not found");
        await Assert.That(rejectionReasonPos >= 0).IsTrue().Because("RejectionReason member not found");
        await Assert.That(rejectionMessagePos >= 0).IsTrue().Because("RejectionMessage member not found");
        await Assert.That(rejectionTimestampPos >= 0).IsTrue().Because("RejectionTimestamp member not found");

        await Assert.That(originalTopicPos < originalTypePos).IsTrue().Because("OriginalTopic must precede OriginalType");
        await Assert.That(originalTypePos < rejectionReasonPos).IsTrue().Because("OriginalType must precede RejectionReason");
        await Assert.That(rejectionReasonPos < rejectionMessagePos).IsTrue().Because("RejectionReason must precede RejectionMessage");
        await Assert.That(rejectionMessagePos < rejectionTimestampPos).IsTrue().Because("RejectionMessage must precede RejectionTimestamp");

        // Assert - record lives in the parent namespace {{ Namespace }}.MessagingGateway{{ Prefix }}
        await Assert.That(content).Contains("namespace MyApp.Tests.MessagingGatewayTest;");
    }

    [Test]
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
        await Assert.That(allEmittedFiles.Length).IsEqualTo(2);

        // Assert - each record lives in its own prefixed parent namespace
        var alphaPath = Path.Combine(_testDirectory, "MessagingGateway", "Alpha", "Generated", "RejectionMetadataKeys.cs");
        var betaPath = Path.Combine(_testDirectory, "MessagingGateway", "Beta", "Generated", "RejectionMetadataKeys.cs");
        await Assert.That(File.Exists(alphaPath)).IsTrue().Because($"Expected Alpha RejectionMetadataKeys.cs at {alphaPath}");
        await Assert.That(File.Exists(betaPath)).IsTrue().Because($"Expected Beta RejectionMetadataKeys.cs at {betaPath}");

        var alphaContent = await File.ReadAllTextAsync(alphaPath);
        await Assert.That(alphaContent).Contains("namespace MyApp.Tests.MessagingGateway.Alpha;");

        var betaContent = await File.ReadAllTextAsync(betaPath);
        await Assert.That(betaContent).Contains("namespace MyApp.Tests.MessagingGateway.Beta;");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
