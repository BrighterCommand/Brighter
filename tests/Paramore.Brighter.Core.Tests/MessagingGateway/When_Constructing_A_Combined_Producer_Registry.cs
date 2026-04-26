using System.Linq;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.MessagingGateway;
public class CombinedProducerRegistryTests
{
    [Test]
    public async Task When_constructing_a_combined_producer_registry()
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
        var producerRegistry = await combinedRegistryFactory.CreateAsync();
        // Producer registry should contain producers for both topics
        var producers = producerRegistry.Producers.ToList();
        await Assert.That(producers.Count).IsEqualTo(2);
        await Assert.That(producers.Count(x => x.Publication.Topic == "FirstTopic")).IsEqualTo(1);
        await Assert.That(producers.Count(x => x.Publication.Topic == "SecondTopic")).IsEqualTo(1);
    }
}