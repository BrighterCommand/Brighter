using System;
using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter
{
    public class ProducerRegistry(Dictionary<RoutingKey, IAmAMessageProducer>? messageProducers) 
        : IAmAProducerRegistry
    {
        private readonly bool _hasProducers = messageProducers != null && messageProducers.Any();
        
        /// <summary>
        /// An iterable list of all the producers in the registry
        /// </summary>
        public IEnumerable<IAmAMessageProducer> Producers { get { return messageProducers is not null ? messageProducers.Values : Array.Empty<IAmAMessageProducer>(); } }
        
        /// <summary>
        /// An iterable list of all the sync producers in the registry
        /// </summary>
        public IEnumerable<IAmAMessageProducerSync> ProducersSync => messageProducers is not null ? messageProducers.Values.Cast<IAmAMessageProducerSync>() : Array.Empty<IAmAMessageProducerSync>(); 

        /// <summary>
        /// An iterable list of all the sync producers in the registry
        /// </summary>
        public IEnumerable<IAmAMessageProducerAsync> ProducersAsync => messageProducers is not null ? messageProducers.Values.Cast<IAmAMessageProducerAsync>() : Array.Empty<IAmAMessageProducerAsync>(); 


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
             if (messageProducers is not null)
             {
                 foreach (var producer in messageProducers)
                 {
                     if (producer.Value is IDisposable disposable)
                         disposable.Dispose();
                 }

                 messageProducers.Clear();
             }
         }


        /// <summary>
        /// Looks up the producer associated with this message via a topic. The topic lives on the message headers
        /// </summary>
        /// <param name="topic">The <see cref="RoutingKey"/> we want to find the producer for</param>
        /// <returns>A producer</returns>
        public IAmAMessageProducer LookupBy(RoutingKey topic)
        {
            if (!_hasProducers)
                throw new ConfigurationException("No producers found in the registry");
            
            return messageProducers![topic];
        }

        /// <summary>
        /// Looks up the producer associated with this message via a topic. The topic lives on the message headers
        /// </summary>
        /// <param name="topic">The <see cref="RoutingKey"/> we want to find the producer for</param>
        /// <returns>A producer</returns>
        public IAmAMessageProducerAsync LookupAsyncBy(RoutingKey topic)
        {
            return (IAmAMessageProducerAsync)LookupBy(topic);
        }
        
        /// <summary>
        /// Looks up the producer associated with this message via a topic. The topic lives on the message headers
        /// </summary>
        /// <param name="topic">The <see cref="RoutingKey"/> we want to find the producer for</param>
        /// <returns>A producer</returns>
        public IAmAMessageProducerSync LookupSyncBy(RoutingKey topic)
        {
            return (IAmAMessageProducerSync)LookupBy(topic);
        }

        /// <summary>
        /// Find the publication for a given request type
        /// </summary>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns></returns>
        /// <exception cref="ConfigurationException">Thrown if we have too many publications or none at all</exception>
        public Publication LookupPublication<TRequest>() where TRequest : class, IRequest
        {
            var publications = from producer in messageProducers
            where producer.Value.Publication.RequestType == typeof(TRequest)
                select producer.Value.Publication;

            var publicationsArray = publications as Publication[] ?? publications.ToArray();
            
            if (publicationsArray.Count() > 1)
                throw new ConfigurationException("Only one producer per request type is supported. Have you added the request type to multiple Publications?");
            
            var publication = publicationsArray.FirstOrDefault();
            
            if (publication is null)
                throw new ConfigurationException("No producer found for request type. Have you set the request type on the Publication?");

            return publication;
        }
    }
}
