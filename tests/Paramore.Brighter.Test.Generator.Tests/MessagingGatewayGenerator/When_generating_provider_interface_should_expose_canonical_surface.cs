using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Test.Generator.Configuration;


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

    [Test]
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

        // Assert — Reactor interface declares the CreateSubscription signature with nullable routing keys
        var reactorInterface = await File.ReadAllTextAsync(
            Path.Combine(reactorOutput, "IAmAMessageGatewayReactorProvider.cs"));
        await Assert.That(reactorInterface).Contains("RoutingKey? deadLetterRoutingKey = null");
        await Assert.That(reactorInterface).Contains("RoutingKey? invalidMessageRoutingKey = null");
        await Assert.That(reactorInterface).DoesNotContain("setupDeadLetterQueue");

        // Assert — Reactor interface exposes the invalid-channel read and the metadata keys
        await Assert.That(reactorInterface).Contains("GetMessageFromInvalidChannel");
        await Assert.That(reactorInterface).Contains("RejectionMetadataKeys RejectionMetadataKeys { get; }");

        // Assert — XML doc states the MT_NONE contract for bounded read members
        await Assert.That(reactorInterface).Contains("MT_NONE");

        // Assert — Proactor interface declares the CreateSubscription signature with nullable routing keys
        var proactorInterface = await File.ReadAllTextAsync(
            Path.Combine(proactorOutput, "IAmAMessageGatewayProactorProvider.cs"));
        await Assert.That(proactorInterface).Contains("RoutingKey? deadLetterRoutingKey = null");
        await Assert.That(proactorInterface).Contains("RoutingKey? invalidMessageRoutingKey = null");
        await Assert.That(proactorInterface).DoesNotContain("setupDeadLetterQueue");

        // Assert — Proactor interface exposes the async invalid-channel read and the metadata keys
        await Assert.That(proactorInterface).Contains("GetMessageFromInvalidChannelAsync");
        await Assert.That(proactorInterface).Contains("Task<Message>");
        await Assert.That(proactorInterface).Contains("CancellationToken");
        await Assert.That(proactorInterface).Contains("RejectionMetadataKeys RejectionMetadataKeys { get; }");

        // Assert — XML doc states the MT_NONE contract for bounded read members
        await Assert.That(proactorInterface).Contains("MT_NONE");

    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}
