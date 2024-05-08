using System.Collections.Generic;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors;

public class ProducerRegistryTests 
{
    [Fact]
    public void When_Merging_Two_Producer_Registries()
    {
        //arrange
        var producerRegistryOne = new ProducerRegistry(
            new Dictionary<string, IAmAMessageProducer>{{"TestOne", new FakeMessageProducer()}}
        );
        
        var producerRegistryTwo = new ProducerRegistry(
            new Dictionary<string, IAmAMessageProducer>{{"TestTwo", new FakeMessageProducer()}}
        );
        
        //act
        var producerRegistry = producerRegistryOne.Merge(producerRegistryTwo);
        
        //assert
        producerRegistry.LookupBy("TestOne").Should().NotBeNull();
        producerRegistry.LookupBy("TestTwo").Should().NotBeNull();
    
    }
}
