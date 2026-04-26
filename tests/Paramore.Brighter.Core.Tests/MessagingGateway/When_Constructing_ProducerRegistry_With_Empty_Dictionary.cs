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
#endregion
using System.Collections.Generic;

namespace Paramore.Brighter.Core.Tests.MessagingGateway;
public class EmptyProducerRegistryTests
{
    [Test]
    public async Task When_constructing_producer_registry_with_empty_dictionary()
    {
        // Arrange
        var emptyProducers = new Dictionary<ProducerKey, IAmAMessageProducer>();
        // Act
        var registry = new ProducerRegistry(emptyProducers);
        // Assert
        await Assert.That(registry).IsNotNull();
        await Assert.That(registry.Producers).IsEmpty();
        await Assert.That(registry.ProducersSync).IsEmpty();
        await Assert.That(registry.ProducersAsync).IsEmpty();
    }

    [Test]
    public async Task When_constructing_producer_registry_with_null_dictionary()
    {
        // Arrange
        Dictionary<ProducerKey, IAmAMessageProducer>? nullProducers = null;
        // Act
        var registry = new ProducerRegistry(nullProducers);
        // Assert
        await Assert.That(registry).IsNotNull();
        await Assert.That(registry.Producers).IsEmpty();
        await Assert.That(registry.ProducersSync).IsEmpty();
        await Assert.That(registry.ProducersAsync).IsEmpty();
    }

    [Test]
    public async Task When_looking_up_producer_in_empty_registry_throws()
    {
        // Arrange
        var emptyProducers = new Dictionary<ProducerKey, IAmAMessageProducer>();
        var registry = new ProducerRegistry(emptyProducers);
        var topic = new RoutingKey("test-topic");
        // Act & Assert
        var exception = await Assert.That(() => registry.LookupBy(topic)).ThrowsExactly<ConfigurationException>();
        await Assert.That(exception.Message).Contains("No producers found in the registry");
    }

    [Test]
    public async Task When_closing_empty_producer_registry_succeeds()
    {
        // Arrange
        var emptyProducers = new Dictionary<ProducerKey, IAmAMessageProducer>();
        var registry = new ProducerRegistry(emptyProducers);
        // Act & Assert (should not throw)
        registry.CloseAll();
        await Assert.That(registry.Producers).IsEmpty();
    }

    [Test]
    public async Task When_disposing_empty_producer_registry_succeeds()
    {
        // Arrange
        var emptyProducers = new Dictionary<ProducerKey, IAmAMessageProducer>();
        var registry = new ProducerRegistry(emptyProducers);
        // Act & Assert (should not throw)
        registry.Dispose();
    }
}