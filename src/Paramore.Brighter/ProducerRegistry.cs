using System;
using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter
{
    public class ProducerRegistry : IAmAProducerRegistry
    {

        private Dictionary<string, IAmAMessageProducer> _messageProducers;
        private readonly bool _hasProducers;

        public ProducerRegistry(Dictionary<string, IAmAMessageProducer> messageProducers)
        {
            _messageProducers = messageProducers;
            _hasProducers = messageProducers != null && messageProducers.Any();
        }

        /// <summary>
        /// Will call CloseAll to terminate producers
        /// </summary>
         public void Dispose()
         {
             CloseAll();
             GC.SuppressFinalize(this);
         }

         ~ProducerRegistry()
         {
             CloseAll();
         }

        /// <summary>
        /// Iterates through all the producers and disposes them, as they may have unmanaged resources that should be shut down in an orderly fashion
        /// </summary>
        public void CloseAll()
        {
            foreach (var producer in _messageProducers)
            {
                producer.Value.Dispose();
            }

            _messageProducers.Clear();
        }

        /// <summary>
        /// Used to obtain values from the first producer for configuration of the Outbox. Workaround because the outbox properties are on the publication
        /// expect to be removed
        /// </summary>
        /// <returns></returns>
        public IAmAMessageProducer GetDefaultProducer()
        {
            //TODO: We have to do this for properties that are across many producers associated with the Outbox and it should move to seperate configuration
            //The Producer Registry could store this, but probably separation of concerns implies Outbox configuration

            if (_hasProducers)
                return _messageProducers.First().Value;

            throw new ConfigurationException("No producers configured for the external service bus");
        }

        /// <summary>
        /// Looks up the producer associated with this message via a topic. The topic lives on the message headers
        /// </summary>
        /// <param name="topic">The topic we want to find the producer for</param>
        /// <returns>A producer</returns>
        public IAmAMessageProducer LookupBy(string topic)
        {
            return _messageProducers[topic];
        }

        /// <summary>
        /// Looks up the producer associated with this message via a topic or returns the default producer. The topic lives on the message headers
        /// </summary>
        /// <param name="topic">The topic we want to find the producer for</param>
        /// <returns>A producer</returns>
        public IAmAMessageProducer LookupByOrDefault(string topic)
        {
            if (_messageProducers.ContainsKey(topic)) return LookupBy(topic);

            return GetDefaultProducer();
        }

        /// <summary>
        /// An iterable list of all the producers in the registry
        /// </summary>
        public IEnumerable<IAmAMessageProducer> Producers { get { return _messageProducers.Values; } }
    }
}
