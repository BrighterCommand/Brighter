using Xunit;

namespace Paramore.Brighter.Core.Tests.Context;

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
        Assert.NotNull(context);
        Assert.NotNull(context.Bag);
        Assert.Null(context.Policies);
        Assert.Null(context.FeatureSwitches);
        Assert.Null(context.Span);
    }
}
