using System.Linq;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Consumer;

public class InMemoryProducerRegistryFactoryTests 
{
    [Fact]
    public void When_creating_an_inmemory_producer_registry()
    {
       // arrange
       var bus = new InternalBus(); 
       var publication = new Publication() { Topic = new RoutingKey("Topic") };
       var inMemoryProducerRegistryFactory = new InMemoryProducerRegistryFactory(bus, new[] { publication }, InstrumentationOptions.All);

       //act
       var producerRegistry = inMemoryProducerRegistryFactory.Create();

       //assert
       Assert.NotNull(producerRegistry);
       Assert.Contains(producerRegistry.Producers, p => p.Publication.Topic == publication.Topic); 
    }
}
