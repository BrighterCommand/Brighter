#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using RabbitMQ.Client;
using Xunit;

namespace Paramore.Brighter.RMQ.Async.Tests.MessagingGateway;

// These tests validate gateway configuration plumbing (ConnectionFactory.Ssl setup) and are
// intentionally hand-written rather than generator-based. See ADR 0035 (Generated Tests).
[Trait("Category", "RMQ")]
[Trait("Category", "MutualTLS")]
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

    [Fact]
    public void When_certificate_is_configured_ssl_is_enabled()
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
        Assert.NotNull(factory.Ssl);
        Assert.True(factory.Ssl.Enabled);
        Assert.Equal("localhost", factory.Ssl.ServerName);
        Assert.NotNull(factory.Ssl.Certs);
        Assert.Single(factory.Ssl.Certs);
        Assert.Same(_testCertificate, factory.Ssl.Certs[0]);
    }

    [Fact]
    public void When_no_certificate_is_configured_ssl_is_not_configured()
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
        Assert.NotNull(factory.Ssl);
        Assert.False(factory.Ssl.Enabled);
    }

    [Fact]
    public void When_certificate_object_and_path_both_set_object_takes_precedence()
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
        Assert.True(factory.Ssl.Enabled);
        Assert.Same(_testCertificate, factory.Ssl.Certs[0]);
    }

    [Fact]
    public void When_certificate_path_is_provided_certificate_is_loaded()
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
        Assert.True(factory.Ssl.Enabled);
        Assert.Single(factory.Ssl.Certs);
        Assert.NotNull(factory.Ssl.Certs[0]);
    }

    [Fact]
    public void When_certificate_file_does_not_exist_throws_file_not_found()
    {
        // Arrange
        var connection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqps://localhost:5671")),
            Exchange = new Exchange("test.exchange"),
            ClientCertificatePath = "/nonexistent/path/cert.pfx"
        };

        // Act & Assert
        var ex = Assert.Throws<FileNotFoundException>(() => new TestableRmqMessageConsumer(connection));
        Assert.Contains("Client certificate file not found", ex.Message);
    }

    [Fact]
    public void When_certificate_file_is_invalid_throws_invalid_operation()
    {
        // Arrange - create a temp file with invalid certificate data
        var invalidCertPath = Path.Combine(Path.GetTempPath(), $"invalid-cert-{Guid.NewGuid()}.pfx");
        File.WriteAllText(invalidCertPath, "not a valid certificate");

        try
        {
            var connection = new RmqMessagingGatewayConnection
            {
                AmpqUri = new AmqpUriSpecification(new Uri("amqps://localhost:5671")),
                Exchange = new Exchange("test.exchange"),
                ClientCertificatePath = invalidCertPath
            };

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => new TestableRmqMessageConsumer(connection));
            Assert.Contains("Failed to load client certificate", ex.Message);
            Assert.Contains("valid .pfx (PKCS#12) certificate", ex.Message);
        }
        finally
        {
            if (File.Exists(invalidCertPath))
                File.Delete(invalidCertPath);
        }
    }

    [Fact]
    public void When_certificate_configuration_is_optional_backwards_compatibility_is_maintained()
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
        Assert.NotNull(factory);
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
