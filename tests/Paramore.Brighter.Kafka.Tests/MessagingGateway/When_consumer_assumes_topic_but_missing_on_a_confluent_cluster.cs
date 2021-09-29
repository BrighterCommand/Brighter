#region Licence
/* The MIT License (MIT)
Copyright © 2014 Wayne Hunsley <whunsley@gmail.com>

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
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Confluent.Kafka;
using FluentAssertions;
using Paramore.Brighter.Kafka.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.Kafka;
using Xunit;
using Xunit.Abstractions;
using SaslMechanism = Paramore.Brighter.MessagingGateway.Kafka.SaslMechanism;
using SecurityProtocol = Paramore.Brighter.MessagingGateway.Kafka.SecurityProtocol;

namespace Paramore.Brighter.Kafka.Tests.MessagingGateway
{
    [Trait("Category", "Kafka")]
    [Trait("Category", "Confluent")]
    [Collection("Kafka")]   //Kafka doesn't like multiple consumers of a partition
    public class KafkaConfluentProducerAssumeTests : IDisposable
    {
        private readonly string _queueName = Guid.NewGuid().ToString(); 
        private readonly string _topic = Guid.NewGuid().ToString();
        private readonly IAmAMessageProducerSync _producer;
        private readonly string _partitionKey = Guid.NewGuid().ToString();

        public KafkaConfluentProducerAssumeTests ()
        {
            string SupplyCertificateLocation()
            {
                //For different platforms, we have to figure out how to get the connection right
                //see: https://docs.confluent.io/platform/current/tutorials/examples/clients/docs/csharp.html
                
                return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "/usr/local/etc/openssl@1.1/cert.pem" : null;
            }
            
            // -- Confluent supply these values, see their .NET examples for your account
            // You need to set those values as environment variables, which we then read, in order
            // to run these tests

            string bootStrapServer = Environment.GetEnvironmentVariable("CONFLUENT_BOOSTRAP_SERVER"); 
            string userName = Environment.GetEnvironmentVariable("CONFLUENT_SASL_USERNAME");
            string password = Environment.GetEnvironmentVariable("CONFLUENT_SASL_PASSWORD");
            
            _producer = new KafkaMessageProducerFactory(
                new KafkaMessagingGatewayConfiguration
                {
                    Name = "Kafka Producer Send Test",
                    BootStrapServers = new[] {bootStrapServer},
                    SecurityProtocol = SecurityProtocol.SaslSsl,
                    SaslMechanisms = SaslMechanism.Plain,
                    SaslUsername = userName,
                    SaslPassword = password,
                    SslCaLocation = SupplyCertificateLocation()
                    
                },
                new KafkaPublication
                {
                    Topic = new RoutingKey(_topic),
                    NumPartitions = 1,
                    ReplicationFactor = 3,
                    //These timeouts support running on a container using the same host as the tests, 
                    //your production values ought to be lower
                    MessageTimeoutMs = 10000,
                    RequestTimeoutMs = 10000,
                    MakeChannels = OnMissingChannel.Assume //This will not make the topic
               }).Create(); 
  
        }

        [Fact]
        public void When_a_consumer_declares_topics_on_a_confluent_cluster()
        {
            var message = new Message(
                new MessageHeader(Guid.NewGuid(), _topic, MessageType.MT_COMMAND)
                {
                    PartitionKey = _partitionKey
                },
                new MessageBody($"test content [{_queueName}]"));

            bool messagePublished = false;
            var producerConfirm = _producer as ISupportPublishConfirmation;
            producerConfirm.OnMessagePublished += delegate(bool success, Guid id)
            {
                if (success) messagePublished = true;
            };
            
            _producer.Send(message);

            //Give this a chance to succeed - will fail
            Task.Delay(5000);

            messagePublished.Should().BeFalse();
        }

        public void Dispose()
        {
            _producer?.Dispose();
        }
    }
}
