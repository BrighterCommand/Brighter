#region Licence
/* The MIT License (MIT)
Copyright © 2015 Wayne Hunsley <whunsley@gmail.com>

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

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    //Maps to Confluent's Kafka's Client Config enum, but we don't want to take a direct dependency on that here
    
    /// <summary>
    /// What is the security protocol used to authenticate the client with the broker
    /// </summary>
    public enum SaslMechanism
    {
        Gssapi,
        Plain,
        ScramSha256,
        ScramSha512,
        OAuthBearer,
    }
    
    public enum SecurityProtocol
    {
        Plaintext,
        Ssl,
        SaslPlaintext,
        SaslSsl,
    }
    
    public class KafkaMessagingGatewayConfiguration
    {
        /// <summary>
        /// Initial list of brokers as a list of broker host or host:port. "
        /// </summary>
        public required string[] BootStrapServers { get; set; }

        /// <summary>
        /// A comma-separated list of debug contexts to enable.  Producer: broker, topic, msg. Consumer: consumer, cgrp, topic, fetch
        /// </summary>
        public string? Debug { get; set; } = null; 

         /// <summary>
         /// Client identifier.
         /// </summary>
         public string? Name { get; set; }

         /// <summary>
         /// The Sasl mechanism by which the client connects to the broker
         /// </summary>
         public SaslMechanism? SaslMechanisms { get; set; } = null;

         /// <summary>
         /// This client's Kerberos principal name.
         /// </summary>
         public string? SaslKerberosPrincipal { get; set; } = null;
         
         /// <summary>
         /// SASL username for use with PLAIN and SASL-SCRAM
         /// </summary>
         public string? SaslUsername { get; set; }
         
         /// <summary>
         /// SASL password for use with PLAIN and SASL-SCRAM 
         /// </summary>
         public string? SaslPassword { get; set; }

         /// <summary>
         /// What is the security protocol used by the broker
         /// </summary>
         public SecurityProtocol? SecurityProtocol { get; set; } = null;

         /// <summary>
         /// Where is the CA certificate located
         /// </summary>
         public string? SslCaLocation { get; set; } = null;

         /// <summary>
         /// Path to client's keystore
         /// </summary>
         public string? SslKeystoreLocation { get; set; } = null;
         
         /// <summary>
         /// Password to keystore
         /// </summary>
         public string? SslKeystorePassword { get; set; } = null;

    }
}
