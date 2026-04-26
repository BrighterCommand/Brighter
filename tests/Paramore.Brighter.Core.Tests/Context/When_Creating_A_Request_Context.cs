namespace Paramore.Brighter.Core.Tests.Context;
public class RequestContextFactoryTests
{
    [Test]
    public async Task When_Creating_A_Request_Context()
    {
        //arrange
        var requestContextFactory = new InMemoryRequestContextFactory();
        //act
        var context = requestContextFactory.Create();
        //assert
        await Assert.That(context).IsNotNull();
        await Assert.That(context.Bag).IsNotNull();
        await Assert.That(context.Policies).IsNull();
        await Assert.That(context.FeatureSwitches).IsNull();
        await Assert.That(context.Span).IsNull();
    }
}