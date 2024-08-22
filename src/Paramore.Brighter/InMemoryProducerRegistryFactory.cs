using System.Collections.Generic;

namespace Paramore.Brighter;

public class InMemoryProducerRegistryFactory(InternalBus bus, IEnumerable<Publication> publications) 
    : IAmAProducerRegistryFactory
{
    public IAmAProducerRegistry Create()
    {
        var producerFactory = new InMemoryMessageProducerFactory(bus, publications);

        return new ProducerRegistry(producerFactory.Create());
    }
}
