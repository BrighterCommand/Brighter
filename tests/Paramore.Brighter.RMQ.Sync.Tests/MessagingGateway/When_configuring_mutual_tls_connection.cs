using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using RabbitMQ.Client;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway;

// These tests validate gateway configuration plumbing (ConnectionFactory.Ssl setup) and are
// intentionally hand-written rather than generator-based. See ADR 0035 (Generated Tests).
[Category("RMQ")]
[Category("MutualTLS")]
public class RmqMutualTlsConnectionConfigurationTests : IDisposable
{
    private readonly string _tempCertPath;
    private readonly X509Certificate2 _testCertificate;

    public RmqMutualTlsConnectionConfigurationTests()
    {
        // Create a self-signed test certificate for testing
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        _testCertificate = request.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1),
            DateTimeOffset.Now.AddDays(1));

        // Export to temporary file for file-based tests
        _tempCertPath = Path.Combine(Path.GetTempPath(), $"test-cert-{Guid.NewGuid()}.pfx");
        File.WriteAllBytes(_tempCertPath, _testCertificate.Export(X509ContentType.Pfx, "test-password"));
    }

    public void Dispose()
    {
        _testCertificate?.Dispose();
        if (File.Exists(_tempCertPath))
            File.Delete(_tempCertPath);
    }

    [Test]
    public async Task When_certificate_is_configured_ssl_is_enabled()
    {
        // Arrange
        var connection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqps://localhost:5671")),
            Exchange = new Exchange("test.exchange"),
            ClientCertificate = _testCertificate
        };

        // Act
        var gateway = new TestableRmqMessageConsumer(connection);
        var factory = gateway.GetConnectionFactory();

        // Assert - SSL should be enabled
        await Assert.That(factory.Ssl).IsNotNull();
        await Assert.That(factory.Ssl.Enabled).IsTrue();
        await Assert.That(factory.Ssl.ServerName).IsEqualTo("localhost");
        await Assert.That(factory.Ssl.Certs).IsNotNull();
        await Assert.That(factory.Ssl.Certs.Count).IsEqualTo(1);
        await Assert.That(factory.Ssl.Certs[0]).IsSameReferenceAs(_testCertificate);
    }

    [Test]
    public async Task When_no_certificate_is_configured_ssl_is_not_configured()
    {
        // Arrange - no certificate configured
        var connection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://localhost:5672")),
            Exchange = new Exchange("test.exchange")
        };

        // Act
        var gateway = new TestableRmqMessageConsumer(connection);
        var factory = gateway.GetConnectionFactory();

        // Assert - SSL should not be enabled (backwards compatibility)
        await Assert.That(factory.Ssl).IsNotNull();
        await Assert.That(factory.Ssl.Enabled).IsFalse();
    }

    [Test]
    public async Task When_certificate_object_and_path_both_set_object_takes_precedence()
    {
        // Arrange - both certificate object and path set
        var connection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqps://localhost:5671")),
            Exchange = new Exchange("test.exchange"),
            ClientCertificate = _testCertificate,
            ClientCertificatePath = "/some/other/path.pfx"
        };

        // Act
        var gateway = new TestableRmqMessageConsumer(connection);
        var factory = gateway.GetConnectionFactory();

        // Assert - Should use the certificate object, not load from path
        await Assert.That(factory.Ssl.Enabled).IsTrue();
        await Assert.That(factory.Ssl.Certs[0]).IsSameReferenceAs(_testCertificate);
    }

    [Test]
    public async Task When_certificate_path_is_provided_certificate_is_loaded()
    {
        // Arrange
        var connection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqps://localhost:5671")),
            Exchange = new Exchange("test.exchange"),
            ClientCertificatePath = _tempCertPath,
            ClientCertificatePassword = "test-password"
        };

        // Act
        var gateway = new TestableRmqMessageConsumer(connection);
        var factory = gateway.GetConnectionFactory();

        // Assert - Certificate should be loaded from file
        await Assert.That(factory.Ssl.Enabled).IsTrue();
        await Assert.That(factory.Ssl.Certs.Count).IsEqualTo(1);
        await Assert.That(factory.Ssl.Certs[0]).IsNotNull();
    }

    [Test]
    public async Task When_certificate_file_does_not_exist_throws_file_not_found()
    {
        // Arrange
        var connection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqps://localhost:5671")),
            Exchange = new Exchange("test.exchange"),
            ClientCertificatePath = "/nonexistent/path/cert.pfx"
        };

        // Act & Assert
        var ex = await Assert.That(() => new TestableRmqMessageConsumer(connection)).ThrowsExactly<FileNotFoundException>();
        await Assert.That(ex.Message).Contains("Client certificate file not found");
    }

    [Test]
    public async Task When_certificate_file_is_invalid_throws_invalid_operation()
    {
        // Arrange - create a temp file with invalid certificate data
        var invalidCertPath = Path.Combine(Path.GetTempPath(), $"invalid-cert-{Guid.NewGuid()}.pfx");
        await File.WriteAllTextAsync(invalidCertPath, "not a valid certificate");

        try
        {
            var connection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqps://localhost:5671")),
                Exchange = new Exchange("test.exchange"),
                ClientCertificatePath = invalidCertPath
            };

            // Act & Assert
            var ex = await Assert.That(() => new TestableRmqMessageConsumer(connection)).ThrowsExactly<InvalidOperationException>();
            await Assert.That(ex.Message).Contains("Failed to load client certificate");
            await Assert.That(ex.Message).Contains("valid .pfx (PKCS#12) certificate");
        }
        finally
        {
            if (File.Exists(invalidCertPath))
                File.Delete(invalidCertPath);
        }
    }

    [Test]
    public async Task When_certificate_configuration_is_optional_backwards_compatibility_is_maintained()
    {
        // Arrange - existing code that doesn't use certificates
        var connection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://localhost:5672")),
            Exchange = new Exchange("test.exchange")
        };

        // Act & Assert - Should not throw, gateway should initialize normally
        var gateway = new TestableRmqMessageConsumer(connection);
        var factory = gateway.GetConnectionFactory();
        await Assert.That(factory).IsNotNull();
    }

    // Test double to expose ConnectionFactory for verification
    private sealed class TestableRmqMessageConsumer : RmqMessageGateway
    {
        public TestableRmqMessageConsumer(RmqMessagingGatewayConnection connection)
            : base(connection)
        {
        }

        public ConnectionFactory GetConnectionFactory()
        {
            // Use reflection to access private _connectionFactory field
            var field = typeof(RmqMessageGateway).GetField("_connectionFactory",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return (ConnectionFactory)field!.GetValue(this)!;
        }
    }
}
