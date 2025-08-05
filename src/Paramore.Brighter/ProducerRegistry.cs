using System;
using System.Collections.Generic;
using System.Linq;

namespace Paramore.Brighter
{
    public class ProducerRegistry : IAmAProducerRegistry
    {
        private readonly bool _hasProducers;
        private readonly Dictionary<ProducerKey, IAmAMessageProducer> _messageProducers = new();

        /// <summary>
        /// Constructs a <see cref="ProducerRegistry"/> from a dictionary of <see cref="RoutingKey"/> and <see cref="IAmAMessageProducer"/> pairs.
        /// For each producer, it checks that the producer has a valid <see cref="Publication"/> and that the <see cref="Publication.Topic"/> is set.
        /// It creates a <see cref="ProducerKey"/> using the topic and an empty <see cref="CloudEventsType"/>, and adds the producer to the registry.
        /// </summary>
        /// <param name="messageProducers">The list of <see cref="IAmAMessageProducer"/> and their <see cref="RoutingKey"/>s</param>
        /// <exception cref="ConfigurationException">The producer is missing information</exception>
        public ProducerRegistry(Dictionary<RoutingKey, IAmAMessageProducer>? messageProducers)
        {
            foreach (var producer in messageProducers?.Values ?? Enumerable.Empty<IAmAMessageProducer>())
            {
                if (producer.Publication is null)
                    throw new ConfigurationException($"Producer {producer.GetType().Name} does not have a Publication set");
                
                if (producer.Publication.Topic is null)
                    throw new ConfigurationException($"Producer {producer.GetType().Name} does not have a Topic set in its Publication");
                
                _messageProducers.Add(new ProducerKey(producer.Publication.Topic, CloudEventsType.Empty), producer);
            }
            _hasProducers = _messageProducers.Any();
        }
        
        /// <summary>
        /// Constructs a <see cref="ProducerRegistry"/> from a dictionary of <see cref="ProducerKey"/> and <see cref="IAmAMessageProducer"/> pairs.
        /// For each producer, it checks that the producer has a valid <see cref="Publication"/> and that the <see cref="Publication.Topic"/> is set.
        /// It adds the producer to the registry using the provided <see cref="ProducerKey"/>.
        /// </summary>
        /// <param name="messageProducers">The list of <see cref="IAmAMessageProducer"/> and their <see cref="ProducerKey"/>s</param>
        /// <exception cref="ConfigurationException"></exception>
        public ProducerRegistry(Dictionary<ProducerKey, IAmAMessageProducer> messageProducers)
        {
            if (messageProducers is null || !messageProducers.Any())
                throw new ConfigurationException("No producers found in the registry");

            foreach (var producer in messageProducers.Values)
            {
                if (producer.Publication is null)
                    throw new ConfigurationException($"Producer {producer.GetType().Name} does not have a Publication set");
                
                if (producer.Publication.Topic is null)
                    throw new ConfigurationException($"Producer {producer.GetType().Name} does not have a Topic set in its Publication");
                
                _messageProducers.Add(new ProducerKey(producer.Publication.Topic, producer.Publication.Type), producer);
            }
            _hasProducers = _messageProducers.Any();
        }

        /// <summary>
        /// An iterable list of all the producers in the registry
        /// </summary>
        public IEnumerable<IAmAMessageProducer> Producers => _messageProducers.Values; 

        /// <summary>
        /// An iterable list of all the sync producers in the registry
        /// </summary>
        public IEnumerable<IAmAMessageProducerSync> ProducersSync => _messageProducers.Values.Cast<IAmAMessageProducerSync>();

        /// <summary>
        /// An iterable list of all the sync producers in the registry
        /// </summary>
        public IEnumerable<IAmAMessageProducerAsync> ProducersAsync => _messageProducers.Values.Cast<IAmAMessageProducerAsync>();


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
                 if (producer.Value is IDisposable disposable)
                     disposable.Dispose();
             }

             _messageProducers.Clear();
         }


         /// <summary>
         /// Looks up the producer associated with this message via a topic. The topic lives on the message headers
         /// </summary>
         /// <param name="topic">The <see cref="RoutingKey"/> we want to find the producer for</param>
         /// <param name="requestType">The <see cref="CloudEventsType"/> of the expected message, may be null</param>
         /// <param name="requestContext">The <see cref="RequestContext"/> whose Destination property can override routing</param>
         /// <returns>A producer</returns>
         public IAmAMessageProducer LookupBy(RoutingKey topic, CloudEventsType? requestType = null, RequestContext? requestContext = null)
        {
            if (!_hasProducers)
                throw new ConfigurationException("No producers found in the registry");
            
            if (topic is null)
                throw new ArgumentNullException(nameof(topic), "Topic cannot be null");

            ProducerKey? producerKey = null;
            // If the request context has a destination, we use that to override the topic
            if (requestContext?.Destination != null)
                producerKey = requestContext.Destination;
            else if (requestType != null)
                producerKey = new ProducerKey(topic, requestType);
            else
                producerKey = new ProducerKey(topic, CloudEventsType.Empty);

            if (_messageProducers.TryGetValue(producerKey, out var messageProducer))
                return messageProducer;

            // If we reach here, we have no producers for the topic
            throw new ConfigurationException("No matching producers found in the registry");
        }

        /// <summary>
        /// Looks up the producer associated with this message via a topic. The topic lives on the message headers
        /// </summary>
        /// <param name="topic">The <see cref="RoutingKey"/> we want to find the producer for</param>
        /// <param name="requestType">The <see cref="CloudEventsType"/> of the expected message, may be null</param>
        /// <returns>A producer</returns>
        public IAmAMessageProducerAsync LookupAsyncBy(RoutingKey topic, CloudEventsType? requestType = null)
        {
            return (IAmAMessageProducerAsync)LookupBy(topic, requestType);
        }
        
        /// <summary>
        /// Looks up the producer associated with this message via a topic. The topic lives on the message headers
        /// </summary>
        /// <param name="topic">The <see cref="RoutingKey"/> we want to find the producer for</param>
        /// <returns>A producer</returns>
        public IAmAMessageProducerSync LookupSyncBy(RoutingKey topic, CloudEventsType? requestType = null)
        {
            return (IAmAMessageProducerSync)LookupBy(topic, requestType);
        }

        /// <summary>
        /// Find the publication for a given request type
        /// </summary>
        /// <typeparam name="TRequest">The type of the request</typeparam>
        /// <returns></returns>
        /// <exception cref="ConfigurationException">Thrown if we have too many publications or none at all</exception>
        public Publication? LookupPublication<TRequest>() where TRequest : class, IRequest
        {
            var publications = _messageProducers?.Values
                .Where(producer => producer.Publication.RequestType == typeof(TRequest))
                .Select(producer => producer.Publication)
                .ToArray() ?? [];

            return publications.Length == 1 ? publications[0] : null;
        }
    }
}
