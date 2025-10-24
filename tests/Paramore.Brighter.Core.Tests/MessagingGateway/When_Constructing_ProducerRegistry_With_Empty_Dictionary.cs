#region License

/* The MIT License (MIT)
Copyright © 2025 Jakub Syty jakub.nekro@gmail.com

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

# endregion

using System.Collections.Generic;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessagingGateway;

public class EmptyProducerRegistryTests
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
