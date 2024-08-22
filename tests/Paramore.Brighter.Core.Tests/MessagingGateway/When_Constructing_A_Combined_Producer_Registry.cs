using System.Linq;
using FluentAssertions;
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

        var firstProducerFactory = new InMemoryMessageProducerFactory(bus, firstProducers);
        var secondProducerFactory = new InMemoryMessageProducerFactory(bus, secondProducers);

        var combinedRegistryFactory = new CombinedProducerRegistryFactory(firstProducerFactory, secondProducerFactory);
        var producerRegistry = combinedRegistryFactory.Create();

        // Producer registry should contain producers for both topics
        var producers = producerRegistry.Producers.ToList();
        producers.Count.Should().Be(2);
        producers.Count(x => x.Publication.Topic == "FirstTopic").Should().Be(1);
        producers.Count(x => x.Publication.Topic == "SecondTopic").Should().Be(1);
    }
}
