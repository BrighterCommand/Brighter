﻿#region Licence
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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    internal class KafkaMessageProducer : KafkaMessagingGateway, IAmAMessageProducerSync, IAmAMessageProducerAsync, ISupportPublishConfirmation
    {
        public event Action<bool, string> OnMessagePublished;
      
        public Publication Publication { get; set; }

        private IProducer<string, byte[]> _producer;
        private readonly IKafkaMessageHeaderBuilder _headerBuilder;
        private readonly ProducerConfig _producerConfig;
        private KafkaMessagePublisher _publisher;
        private bool _hasFatalProducerError = false;
        private bool _disposedValue;

        public KafkaMessageProducer(
            KafkaMessagingGatewayConfiguration configuration, 
            KafkaPublication publication)
        {
            if (publication == null)
                throw new ArgumentNullException(nameof(publication));
            
            if (string.IsNullOrEmpty(publication.Topic))
                throw new ConfigurationException("Topic is required for a publication");

            Publication = publication;

            _clientConfig = new ClientConfig
            {
                Acks = (Confluent.Kafka.Acks)((int)publication.Replication),
                BootstrapServers = string.Join(",", configuration.BootStrapServers),
                ClientId = configuration.Name,
                Debug = configuration.Debug,
                SaslMechanism = configuration.SaslMechanisms.HasValue ? (Confluent.Kafka.SaslMechanism?)((int)configuration.SaslMechanisms.Value) : null,
                SaslKerberosPrincipal = configuration.SaslKerberosPrincipal,
                SaslUsername = configuration.SaslUsername,
                SaslPassword = configuration.SaslPassword,
                SecurityProtocol = configuration.SecurityProtocol.HasValue ? (Confluent.Kafka.SecurityProtocol?)((int)configuration.SecurityProtocol.Value) : null,
                SslCaLocation = configuration.SslCaLocation,
                SslKeyLocation = configuration.SslKeystoreLocation,

            };

            //We repeat properties because copying them from them to the producer config updates in client config in place
            _producerConfig = new ProducerConfig()
            {
                Acks = (Confluent.Kafka.Acks)((int)publication.Replication),
                BootstrapServers = string.Join(",", configuration.BootStrapServers),
                ClientId = configuration.Name,
                Debug = configuration.Debug,
                SaslMechanism = configuration.SaslMechanisms.HasValue ? (Confluent.Kafka.SaslMechanism?)((int)configuration.SaslMechanisms.Value) : null,
                SaslKerberosPrincipal = configuration.SaslKerberosPrincipal,
                SaslUsername = configuration.SaslUsername,
                SaslPassword = configuration.SaslPassword,
                SecurityProtocol = configuration.SecurityProtocol.HasValue ? (Confluent.Kafka.SecurityProtocol?)((int)configuration.SecurityProtocol.Value) : null,
                SslCaLocation = configuration.SslCaLocation,
                SslKeyLocation = configuration.SslKeystoreLocation,
                BatchNumMessages = publication.BatchNumberMessages,
                EnableIdempotence = publication.EnableIdempotence,
                EnableDeliveryReports = true,   //don't change this, we need it for the callback
                MaxInFlight = publication.MaxInFlightRequestsPerConnection,
                LingerMs = publication.LingerMs,
                MessageTimeoutMs = publication.MessageTimeoutMs,
                MessageSendMaxRetries = publication.MessageSendMaxRetries,
                Partitioner = (Confluent.Kafka.Partitioner)((int)publication.Partitioner),
                QueueBufferingMaxMessages = publication.QueueBufferingMaxMessages,
                QueueBufferingMaxKbytes = publication.QueueBufferingMaxKbytes,
                RequestTimeoutMs = publication.RequestTimeoutMs,
                RetryBackoffMs = publication.RetryBackoff,
                TransactionalId = publication.TransactionalId,
            };

            MakeChannels = publication.MakeChannels;
            Topic = publication.Topic;
            NumPartitions = publication.NumPartitions;
            ReplicationFactor = publication.ReplicationFactor;
            TopicFindTimeoutMs = publication.TopicFindTimeoutMs;
            _headerBuilder = publication.MessageHeaderBuilder;
        }

        /// <summary>
        /// There are a **lot** of properties that we can set to configure Kafka. We expose only those of high importance
        /// This gives you a chance to set additional parameter before we create the producer. Because it depends on the Confluent
        /// client, we recommend using our version of the properties, which would persist if we changed clients.
        /// This is for properties we **don't** expose
        /// </summary>
        /// <param name="configHook"></param>
        public void ConfigHook(Action<ProducerConfig> configHook)
        {
            configHook(_producerConfig);
        }

        /// <summary>
        /// Initialize the producer => two stage construction to allow for a hook if needed
        /// </summary>
        public void Init()
        {
            _producer = new ProducerBuilder<string, byte[]>(_producerConfig)
                .SetErrorHandler((_, error) =>
                {
                    s_logger.LogError("Code: {ErrorCode}, Reason: {ErrorMessage}, Fatal: {FatalError}", error.Code, error.Reason,
                        error.IsFatal);
                    _hasFatalProducerError = error.IsFatal;
                })
                .Build();
            _publisher = new KafkaMessagePublisher(_producer, _headerBuilder);

            EnsureTopic();
        }
 

        public void Send(Message message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (_hasFatalProducerError)
                throw new ChannelFailureException($"Producer is in unrecoverable state");

            try
            {
                s_logger.LogDebug(
                    "Sending message to Kafka. Servers {Servers} Topic: {Topic} Body: {Request}",
                    _producerConfig.BootstrapServers,
                    message.Header.Topic,
                    message.Body.Value
                );

                _publisher.PublishMessage(message, report => PublishResults(report.Status, report.Headers));

            }
            catch (ProduceException<string, string> pe)
            {
                s_logger.LogError(pe,
                    "Error sending message to Kafka servers {Servers} because {ErrorMessage} ",
                    _producerConfig.BootstrapServers,
                    pe.Error.Reason
                );
                throw new ChannelFailureException("Error talking to the broker, see inner exception for details", pe);
            }
            catch (InvalidOperationException ioe)
            {
                s_logger.LogError(ioe,
                    "Error sending message to Kafka servers {Servers} because {ErrorMessage} ",
                    _producerConfig.BootstrapServers,
                    ioe.Message
                );
                throw new ChannelFailureException("Error talking to the broker, see inner exception for details", ioe);

            }
            catch (ArgumentException ae)
            {
                s_logger.LogError(ae,
                    "Error sending message to Kafka servers {Servers} because {ErrorMessage} ",
                    _producerConfig.BootstrapServers,
                    ae.Message
                );
                throw new ChannelFailureException("Error talking to the broker, see inner exception for details", ae);
               
            }
            catch (KafkaException kafkaException)
            {
                s_logger.LogError(kafkaException, $"KafkaMessageProducer: There was an error sending to topic {Topic})");
                
                if (kafkaException.Error.IsFatal) //this can't be recovered and requires a new producer
                    throw;
                
                throw new ChannelFailureException("Error connecting to Kafka, see inner exception for details", kafkaException);
            }
        }
        
        public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            //No delay support implemented
            Send(message);
        }
        
        
        public async Task SendAsync(Message message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (_hasFatalProducerError)
                 throw new ChannelFailureException($"Producer is in unrecoverable state");
            try
            {
                s_logger.LogDebug(
                    "Sending message to Kafka. Servers {Servers} Topic: {Topic} Body: {Request}",
                    _producerConfig.BootstrapServers,
                    message.Header.Topic,
                    message.Body.Value
                );

                await _publisher.PublishMessageAsync(message, result => PublishResults(result.Status, result.Headers) );

            }
            catch (ProduceException<string, string> pe)
            {
                s_logger.LogError(pe,
                    "Error sending message to Kafka servers {Servers} because {ErrorMessage} ",
                    _producerConfig.BootstrapServers,
                    pe.Error.Reason
                );
                throw new ChannelFailureException("Error talking to the broker, see inner exception for details", pe);
            }
            catch (InvalidOperationException ioe)
            {
                s_logger.LogError(ioe,
                    "Error sending message to Kafka servers {Servers} because {ErrorMessage} ",
                    _producerConfig.BootstrapServers,
                    ioe.Message
                );
                throw new ChannelFailureException("Error talking to the broker, see inner exception for details", ioe);

            }
            catch (ArgumentException ae)
            {
                 s_logger.LogError(ae,
                     "Error sending message to Kafka servers {Servers} because {ErrorMessage} ",
                     _producerConfig.BootstrapServers,
                     ae.Message
                 );
                 throw new ChannelFailureException("Error talking to the broker, see inner exception for details", ae);
               
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (_producer != null)
                    {
                        _producer.Flush(TimeSpan.FromMilliseconds(_producerConfig.MessageTimeoutMs.Value + 5000)); 
                        _producer.Dispose();
                        _producer = null;
                    }
                }

                _disposedValue = true;
            }
        }

        ~KafkaMessageProducer()
        {
           Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        private void PublishResults(PersistenceStatus status, Headers headers)
        {
            if (status == PersistenceStatus.Persisted)
            {
                if (headers.TryGetLastBytesIgnoreCase(HeaderNames.MESSAGE_ID, out byte[] messageIdBytes))
                {
                    var val = messageIdBytes.FromByteArray();
                    if (!string.IsNullOrEmpty(val))
                    {
                        Task.Run(() => OnMessagePublished?.Invoke(true, val));
                        return;
                    }
                }
            }
            
            Task.Run((() =>OnMessagePublished?.Invoke(false, string.Empty)));
        }
    }
}
