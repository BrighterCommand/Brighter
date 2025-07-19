using System;
using Paramore.Brighter.InMemory.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.InMemory.Tests.Consumer;

public class InMemoryChannelFactoryTests 
{
    [Fact]
    public void When_an_inmemory_channelfactory_is_called()
    {
        //arrange
        var internalBus = new InternalBus();
        var inMemoryChannelFactory = new InMemoryChannelFactory(internalBus, TimeProvider.System);
        
        //act
        var channel = inMemoryChannelFactory.CreateSyncChannel(new Subscription<MyEvent>(messagePumpType: MessagePumpType.Reactor));
        
        //assert
        Assert.NotNull(channel);
    }
}
