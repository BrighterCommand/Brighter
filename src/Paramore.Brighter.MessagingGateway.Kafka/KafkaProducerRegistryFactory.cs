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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    /// <summary>
    /// Creates a registry of <see cref="KafkaMessageProducer"/> instances from <see cref="Publication"/> instances
    /// Note that we only return the interface and <see cref="KafkaMessageProducer"/> is internal as the underlying type is not needed
    /// </summary>
    public class KafkaProducerRegistryFactory : IAmAProducerRegistryFactory
    {
        private readonly KafkaMessagingGatewayConfiguration _globalConfiguration;
        private readonly IEnumerable<KafkaPublication> _publications;
        private Action<ProducerConfig>? _configHook;

        /// <summary>
        /// This constructs a <see cref="KafkaProducerRegistryFactory"/> which can be used to create a <see cref="ProducerRegistry" /> of <see cref="KafkaMessageProducer" /> instances.
        /// It takes a dependency on a <see cref="KafkaMessagingGatewayConfiguration"/> to connect to the broker, and a collection of <see cref="KafkaPublication"/> instances
        /// that determine how we publish to Kafka and the parameters of any topics if required.
        /// </summary>
        /// <param name="globalConfiguration">Configures how we connect to the broker</param>
        /// <param name="publications">The list of topics that we want to publish to</param>
        public KafkaProducerRegistryFactory(
            KafkaMessagingGatewayConfiguration globalConfiguration, 
            IEnumerable<KafkaPublication> publications)
        {
            _globalConfiguration = globalConfiguration;
            _publications = publications;
            _configHook = null;
        }
        
        /// <summary>
        /// Create a producer registry from the <see cref="KafkaMessagingGatewayConfiguration"/> and <see cref="KafkaPublication"/> instances supplied
        /// to the constructor
        /// </summary>
        /// <returns>An <see cref="IAmAProducerRegistry"/> that represents a collection of Kafka Message Producers</returns>
        public IAmAProducerRegistry Create()
        {
            var producerFactory = new KafkaMessageProducerFactory(_globalConfiguration, _publications);
            producerFactory.SetConfigHook(_configHook);

            return new ProducerRegistry(producerFactory.Create());
        }

        /// <summary>
        /// Create a producer registry from the <see cref="KafkaMessagingGatewayConfiguration"/> and <see cref="KafkaPublication"/> instances supplied
        /// to the constructor
        /// </summary>
        /// <returns>An <see cref="IAmAProducerRegistry"/> that represents a collection of Kafka Message Producers</returns>
        public Task<IAmAProducerRegistry> CreateAsync(CancellationToken ct = default)
        {
            return Task.FromResult(Create());    
        }

        /// <summary>
        /// Set a configuration hook to set properties not exposed by KafkaMessagingGatewayConfiguration or KafkaPublication
        /// Intended as 'get out of gaol free' this couples us to the Confluent .NET Kafka client. Bear in mind that a future release
        /// might drop the Confluent client, and this hook
        /// </summary>
        /// <param name="hook"></param>
        public void SetConfigHook(Action<ProducerConfig>? hook)
        {
            _configHook = hook;
        }
    }
}
