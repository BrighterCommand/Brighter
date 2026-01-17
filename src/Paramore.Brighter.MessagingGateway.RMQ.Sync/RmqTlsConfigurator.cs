#region Licence
/* The MIT License (MIT)
Copyright © 2024 Darren Schwarz <darrenschwarz@gmail.com>

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
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using RabbitMQ.Client;

namespace Paramore.Brighter.MessagingGateway.RMQ.Sync;

/// <summary>
/// Configures TLS/SSL settings for RabbitMQ connections with mutual authentication support.
/// </summary>
internal static class RmqTlsConfigurator
{
    internal static void ConfigureIfEnabled(ConnectionFactory connectionFactory, RmqMessagingGatewayConnection connection)
    {
        var certificate = LoadCertificate(connection);

        if (certificate != null)
        {
            var sslOption = new SslOption
            {
                Enabled = true,
                ServerName = connectionFactory.Uri.Host,
                Certs = new X509CertificateCollection { certificate }
            };

            // Trust self-signed certificates if configured (for test/development environments)
            // Bypasses both chain validation errors AND hostname mismatches
            if (connection.TrustServerSelfSignedCertificate)
            {
                sslOption.AcceptablePolicyErrors = SslPolicyErrors.RemoteCertificateChainErrors |
                                                    SslPolicyErrors.RemoteCertificateNameMismatch;
            }

            connectionFactory.Ssl = sslOption;
        }
    }

    private static X509Certificate2? LoadCertificate(RmqMessagingGatewayConnection connection)
    {
        // Precedence: ClientCertificate object takes precedence over file path
        if (connection.ClientCertificate != null)
            return connection.ClientCertificate;

        if (!string.IsNullOrEmpty(connection.ClientCertificatePath))
        {
            if (!File.Exists(connection.ClientCertificatePath))
                throw new FileNotFoundException(
                    $"RMQMessagingGateway: Client certificate file not found: {connection.ClientCertificatePath}",
                    connection.ClientCertificatePath);

            try
            {
                // Load certificate with password if provided, otherwise load without password
#if NET9_0_OR_GREATER
                return string.IsNullOrEmpty(connection.ClientCertificatePassword)
                    ? X509CertificateLoader.LoadPkcs12FromFile(connection.ClientCertificatePath, null)
                    : X509CertificateLoader.LoadPkcs12FromFile(connection.ClientCertificatePath, connection.ClientCertificatePassword);
#else
                return string.IsNullOrEmpty(connection.ClientCertificatePassword)
                    ? new X509Certificate2(connection.ClientCertificatePath)
                    : new X509Certificate2(connection.ClientCertificatePath, connection.ClientCertificatePassword);
#endif
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"RMQMessagingGateway: Failed to load client certificate from {connection.ClientCertificatePath}. " +
                    $"Ensure the file is a valid .pfx (PKCS#12) certificate and the password is correct.",
                    ex);
            }
        }

        return null;
    }
}
