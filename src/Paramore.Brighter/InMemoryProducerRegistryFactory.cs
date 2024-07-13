using System;
using System.Collections.Generic;

namespace Paramore.Brighter;

public class InMemoryProducerRegistryFactory(InternalBus bus, IEnumerable<Publication> publications) : IAmAProducerRegistryFactory
{
    public IAmAProducerRegistry Create()
    {
        var producers = new Dictionary<string, IAmAMessageProducer>();
        foreach (var publication in publications)
        {
            var producer = new InMemoryProducer(bus, TimeProvider.System);
            producer.Publication = publication;
            producers[publication.Topic] = producer;
        }

        return new ProducerRegistry(producers);
    }
}
