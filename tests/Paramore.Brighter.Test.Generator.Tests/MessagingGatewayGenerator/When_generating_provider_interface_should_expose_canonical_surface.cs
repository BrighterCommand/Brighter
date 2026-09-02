using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;
using Xunit;

namespace Paramore.Brighter.Test.Generator.Tests.MessagingGatewayGenerator;

public class WhenGeneratingProviderInterfaceShouldExposeCanonicalSurface : IDisposable
{
    private readonly string _testDirectory;
    private readonly ILogger<Generators.MessagingGatewayGenerator> _logger;

    public WhenGeneratingProviderInterfaceShouldExposeCanonicalSurface()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"MessagingGatewayGeneratorTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var factory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = factory.CreateLogger<Generators.MessagingGatewayGenerator>();
    }

    [Fact]
    public async Task When_generating_provider_interface_should_expose_canonical_surface()
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
                HasSupportToDeadLetterQueue = true,
                HasSupportToRequeue = true,
                HasSupportToDelayedMessages = false,
                HasSupportToPublishConfirmation = false,
                HasSupportToValidateBrokerExistence = false,
                HasSupportToValidateInfrastructure = false,
            }
        };
        var generator = new Generators.MessagingGatewayGenerator(_logger);
        var reactorOutput = Path.Combine(_testDirectory, "MessagingGateway", "Test", "Generated", "Reactor");
        var proactorOutput = Path.Combine(_testDirectory, "MessagingGateway", "Test", "Generated", "Proactor");

        // Act
        await generator.GenerateAsync(configuration);

        // Assert — Reactor interface declares the FR-1(1)(2) CreateSubscription signature with nullable routing keys
        var reactorInterface = await File.ReadAllTextAsync(
            Path.Combine(reactorOutput, "IAmAMessageGatewayReactorProvider.cs"));
        Assert.Contains("RoutingKey? deadLetterRoutingKey = null", reactorInterface);
        Assert.Contains("RoutingKey? invalidMessageRoutingKey = null", reactorInterface);
        Assert.DoesNotContain("setupDeadLetterQueue", reactorInterface);

        // Assert — Reactor interface exposes FR-1(3) invalid-channel read and FR-1(5) metadata keys
        Assert.Contains("GetMessageFromInvalidChannel", reactorInterface);
        Assert.Contains("RejectionMetadataKeys RejectionMetadataKeys { get; }", reactorInterface);

        // Assert — XML doc states the MT_NONE contract for bounded read members (FR-1(3))
        Assert.Contains("MT_NONE", reactorInterface);

        // Assert — Proactor interface declares the FR-1(1)(2) CreateSubscription signature with nullable routing keys
        var proactorInterface = await File.ReadAllTextAsync(
            Path.Combine(proactorOutput, "IAmAMessageGatewayProactorProvider.cs"));
        Assert.Contains("RoutingKey? deadLetterRoutingKey = null", proactorInterface);
        Assert.Contains("RoutingKey? invalidMessageRoutingKey = null", proactorInterface);
        Assert.DoesNotContain("setupDeadLetterQueue", proactorInterface);

        // Assert — Proactor interface exposes async FR-1(3) invalid-channel read and FR-1(5) metadata keys
        Assert.Contains("GetMessageFromInvalidChannelAsync", proactorInterface);
        Assert.Contains("Task<Message>", proactorInterface);
        Assert.Contains("CancellationToken", proactorInterface);
        Assert.Contains("RejectionMetadataKeys RejectionMetadataKeys { get; }", proactorInterface);

        // Assert — XML doc states the MT_NONE contract for bounded read members (FR-1(3))
        Assert.Contains("MT_NONE", proactorInterface);

    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
