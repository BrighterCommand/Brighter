using System;
using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter
{
    public class ProducerRegistry(Dictionary<string, IAmAMessageProducer> messageProducers) : IAmAProducerRegistry
    {
        private readonly bool _hasProducers = messageProducers != null && messageProducers.Any();

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
            foreach (var producer in messageProducers)
            {
                producer.Value.Dispose();
            }
            
            messageProducers.Clear();
        }


        /// <summary>
        /// Looks up the producer associated with this message via a topic. The topic lives on the message headers
        /// </summary>
        /// <param name="topic">The topic we want to find the producer for</param>
        /// <returns>A producer</returns>
        public IAmAMessageProducer LookupBy(string topic)
        {
            return messageProducers[topic];
        }

        public Publication LookupPublication(string topic)
        {
            return LookupBy(topic).Publication;
        }

        /// <summary>
        /// An iterable list of all the producers in the registry
        /// </summary>
        public IEnumerable<IAmAMessageProducer> Producers { get { return messageProducers.Values; } }
    }
}
