using System.Linq;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.InMemory.Tests.Consumer;

public class InMemoryProducerRegistryFactoryTests
{
    [Test]
    public async Task When_creating_an_inmemory_producer_registry()
    {
       // arrange
       var bus = new InternalBus();
       var publication = new Publication() { Topic = new RoutingKey("Topic") };
       var inMemoryProducerRegistryFactory = new InMemoryProducerRegistryFactory(bus, [publication], InstrumentationOptions.All);

       //act
       var producerRegistry = await inMemoryProducerRegistryFactory.CreateAsync();

       //assert
       await Assert.That(producerRegistry).IsNotNull();
       await Assert.That((producerRegistry.Producers).Any(p => p.Publication.Topic == publication.Topic)).IsTrue();
    }
}
