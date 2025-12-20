#region Licence

/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Security.Cryptography.X509Certificates;
using RabbitMQ.Client;

namespace Paramore.Brighter.MessagingGateway.RMQ.Sync
{
    public class RmqMessagingGatewayConnection : IAmGatewayConfiguration 
    {
      public RmqMessagingGatewayConnection()
      {
        Name = Environment.MachineName;
      }

      /// <summary>
      /// Sets Unique name for the subscription
      /// </summary>
      public string? Name { get; set; }

      /// <summary>
      /// Gets or sets the ampq URI.
      /// </summary>
      /// <value>The ampq URI.</value>
      public AmqpUriSpecification? AmpqUri { get; set; }

        /// <summary>
        /// Gets or sets the exchange.
        /// </summary>
        /// <value>The exchange.</value>
        public Exchange? Exchange { get; set; }

        /// <summary>
        /// The exchange used for any dead letter queue
        /// </summary>
        public Exchange? DeadLetterExchange { get; set; }
        
        /// <summary>
        /// Gets or sets the Heartbeat in seconds. Defaults to 20.
        /// </summary>
        public ushort Heartbeat { get; set; } = 20;

        /// <summary>
        /// Gets or sets whether to persist messages. Defaults to false.
        /// </summary>
        public bool PersistMessages { get; set; }

        /// <summary>
        ///     Gets or sets RabbitMq protocol timeouts, in seconds. Defaults to 20s.
        ///     <see cref="ConnectionFactory.ContinuationTimeout" /> for more information.
        /// </summary>
        public ushort ContinuationTimeout { get; set; } = 20;

        /// <summary>
        /// Gets or sets the client certificate for mutual TLS authentication.
        /// Optional - if not provided, connection will not use client certificates.
        /// Takes precedence over <see cref="ClientCertificatePath"/> if both are set.
        /// </summary>
        /// <value>The X509 client certificate.</value>
        public X509Certificate2? ClientCertificate { get; set; }

        /// <summary>
        /// Gets or sets the file path to the client certificate for mutual TLS authentication.
        /// Supports .pfx (PKCS#12) format.
        /// Optional - if not provided, connection will not use client certificates.
        /// </summary>
        /// <value>The path to the certificate file.</value>
        public string? ClientCertificatePath { get; set; }

        /// <summary>
        /// Gets or sets the password for the client certificate file.
        /// Only used when <see cref="ClientCertificatePath"/> is provided and the certificate is password-protected.
        /// </summary>
        /// <value>The certificate password.</value>
        public string? ClientCertificatePassword { get; set; }
    }

    /// <summary>
    /// Class AMQPUriSpecification
    /// </summary>
    public class AmqpUriSpecification
    {
        private string? _sanitizedUri;

        public AmqpUriSpecification(Uri uri, int connectionRetryCount = 3, int retryWaitInMilliseconds = 1000, int circuitBreakTimeInMilliseconds = 60000)
        {
            Uri = uri;
            ConnectionRetryCount = connectionRetryCount;
            RetryWaitInMilliseconds = retryWaitInMilliseconds;
            CircuitBreakTimeInMilliseconds = circuitBreakTimeInMilliseconds;
        }
        /// <summary>
        /// Gets or sets the URI.
        /// </summary>
        /// <value>The URI.</value>
        public Uri Uri { get; set; }


        /// <summary>
        /// Gets or sets the retry count for when a subscription fails
        /// </summary>
        public int ConnectionRetryCount { get; set; }

        /// <summary>
        /// The time in milliseconds to wait before retrying to connect again
        /// </summary>
        public int RetryWaitInMilliseconds { get; set; }

        /// <summary>
        /// The time in milliseconds to wait before retrying to connect again. 
        /// </summary>
        public int CircuitBreakTimeInMilliseconds { get; set; }

        public string GetSanitizedUri()
        {
            if (_sanitizedUri != null) return _sanitizedUri;

            var uri = Uri.ToString();
            var positionOfSlashSlash = uri.IndexOf("//", StringComparison.Ordinal) + 2;
            var usernameAndPassword = uri.Substring(positionOfSlashSlash, uri.IndexOf('@') - positionOfSlashSlash);
            _sanitizedUri = uri.Replace(usernameAndPassword, "*****");

            return _sanitizedUri;
        }
    }

    /// <summary>
    /// Class Exchange.
    /// </summary>
    public class Exchange
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>The name.</value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the type. DefaultValue = ExchangeType.Direct
        /// </summary>
        /// <value>The type.</value>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Exchange"/> is durable.
        /// </summary>
        /// <value><c>true</c> if durable; otherwise, <c>false</c>.</value>
        public bool Durable { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if the declared <see cref="Exchange"/> support delayed messages.
        /// (requires plugin rabbitmq_delayed_message_exchange)
        /// </summary>
        /// <value><c>true</c> if supporting; otherwise, <c>false</c>.</value>
        public bool SupportDelay { get; set; }

        public Exchange(string name, string type = ExchangeType.Direct, bool durable = false, bool supportDelay = false)
        {
            Name = name;
            Type = type;
            Durable = durable;
            SupportDelay = supportDelay;
        }
    }
}
