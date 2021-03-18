using System;
using Xunit;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;

namespace Paramore.Brighter.AzureServiceBus.Tests
{
    
    public class AzureServiceBusChannelFactoryTests
    {
        [Fact]
        public void When_the_timeout_is_below_400_ms_it_should_throw_an_exception()
        {
            var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(new AzureServiceBusConfiguration("someString")));

            var subscription = new AzureServiceBusSubscription(typeof(object), new SubscriptionName("name"), new ChannelName("name"), new RoutingKey("name"),
                1, 1, 399);
            
            ArgumentException exception = Assert.Throws<ArgumentException>(() => factory.CreateChannel(subscription));

            Assert.Equal("The minimum allowed timeout is 400 milliseconds", exception.Message);
        }
    }
}
