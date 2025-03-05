using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Bus;

public class InternalBusStreamTests 
{
    [Fact]
    public void When_accessing_messages_on_an_empty_bus()
    {
         //arrange
         //creste an instance of the InternalBus
         var internalBus = new InternalBus();
         
         //act
         //try to stream from it
         var messages = internalBus.Stream(new RoutingKey("test"));
         
        //assert
        //should be empty 
        Assert.Empty(messages);
    }
}
