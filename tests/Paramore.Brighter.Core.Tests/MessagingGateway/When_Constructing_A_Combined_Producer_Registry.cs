using System.Linq;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessagingGateway;

public class CombinedProducerRegistryTests
{
    [Fact]
    public void When_constructing_a_combined_producer_registry()
    {
        var bus = new InternalBus();

        var firstProducers = new[]
        {
            new Publication
            {
                Topic = new RoutingKey("FirstTopic")
            }
        };
        var secondProducers = new[]
        {
            new Publication
            {
                Topic = new RoutingKey("SecondTopic")
            }
        };

        var firstProducerFactory = new InMemoryMessageProducerFactory(bus, firstProducers, InstrumentationOptions.All);
        var secondProducerFactory = new InMemoryMessageProducerFactory(bus, secondProducers, InstrumentationOptions.All);

        var combinedRegistryFactory = new CombinedProducerRegistryFactory(firstProducerFactory, secondProducerFactory);
        var producerRegistry = combinedRegistryFactory.Create();

        // Producer registry should contain producers for both topics
        var producers = producerRegistry.Producers.ToList();
        Assert.Equal(2, producers.Count);
        Assert.Equal(1, producers.Count(x => x.Publication.Topic == "FirstTopic"));
        Assert.Equal(1, producers.Count(x => x.Publication.Topic == "SecondTopic"));
    }
}
