using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessagingGateway;

public class When_Constructing_ProducerRegistry_With_Empty_Dictionary
{
    [Fact]
    public void When_constructing_producer_registry_with_empty_dictionary()
    {
        // Arrange
        var emptyProducers = new Dictionary<ProducerKey, IAmAMessageProducer>();

        // Act
        var registry = new ProducerRegistry(emptyProducers);

        // Assert
        Assert.NotNull(registry);
        Assert.Empty(registry.Producers);
        Assert.Empty(registry.ProducersSync);
        Assert.Empty(registry.ProducersAsync);
    }

    [Fact]
    public void When_constructing_producer_registry_with_null_dictionary()
    {
        // Arrange
        Dictionary<ProducerKey, IAmAMessageProducer>? nullProducers = null;

        // Act
        var registry = new ProducerRegistry(nullProducers);

        // Assert
        Assert.NotNull(registry);
        Assert.Empty(registry.Producers);
        Assert.Empty(registry.ProducersSync);
        Assert.Empty(registry.ProducersAsync);
    }

    [Fact]
    public void When_looking_up_producer_in_empty_registry_throws()
    {
        // Arrange
        var emptyProducers = new Dictionary<ProducerKey, IAmAMessageProducer>();
        var registry = new ProducerRegistry(emptyProducers);
        var topic = new RoutingKey("test-topic");

        // Act & Assert
        var exception = Assert.Throws<ConfigurationException>(() =>
            registry.LookupBy(topic));
        Assert.Contains("No producers found in the registry", exception.Message);
    }

    [Fact]
    public void When_closing_empty_producer_registry_succeeds()
    {
        // Arrange
        var emptyProducers = new Dictionary<ProducerKey, IAmAMessageProducer>();
        var registry = new ProducerRegistry(emptyProducers);

        // Act & Assert (should not throw)
        registry.CloseAll();
        Assert.Empty(registry.Producers);
    }

    [Fact]
    public void When_disposing_empty_producer_registry_succeeds()
    {
        // Arrange
        var emptyProducers = new Dictionary<ProducerKey, IAmAMessageProducer>();
        var registry = new ProducerRegistry(emptyProducers);

        // Act & Assert (should not throw)
        registry.Dispose();
    }

}
