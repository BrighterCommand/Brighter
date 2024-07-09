using FluentAssertions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using Xunit;

namespace Paramore.Brighter.Core.Tests.RequestContextFactory;

public class RequestContextFactoryTests
{
    [Fact]
    public void When_Creating_A_Request_Context()
    {
        //arrange
        var requestContextFactory = new InMemoryRequestContextFactory();
        
        //act
        var context = requestContextFactory.Create();
        
        //assert
        context.Should().NotBeNull();
        context.Bag.Should().NotBeNull();
        context.Policies.Should().BeNull();
        context.FeatureSwitches.Should().BeNull();
        context.Span.Should().BeNull();
    }
}
