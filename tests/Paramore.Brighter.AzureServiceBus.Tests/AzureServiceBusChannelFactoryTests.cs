using System;
using System.Threading.Tasks;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;

namespace Paramore.Brighter.AzureServiceBus.Tests;

public class AzureServiceBusChannelFactoryTests
{
    [Test]
    public async Task When_the_timeout_is_below_400_ms_it_should_throw_an_exception()
    {
        var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(new AzureServiceBusConfiguration("Endpoint=sb://someString.servicebus.windows.net;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=oUWJw7777s7ydjdafqFqhk9O7TOs=")));

        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(new SubscriptionName("name"), new ChannelName("name"), new RoutingKey("name"),
            bufferSize: 1, noOfPerformers: 1, messagePumpType: MessagePumpType.Proactor, timeOut: TimeSpan.FromMilliseconds(399));
            
 var exception = Assert.ThrowsExactly<ArgumentException>(() => factory.CreateSyncChannel(subscription));

        await Assert.That(exception.Message).IsEqualTo("The minimum allowed timeout is 400 milliseconds");
    }
        
    [Test]
    public async Task When_the_timeout_is_below_400_ms_it_should_throw_an_exception_async_channel()
    {
        var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(new AzureServiceBusConfiguration("Endpoint=sb://someString.servicebus.windows.net;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=oUWJw7777s7ydjdafqFqhk9O7TOs=")));

        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(new SubscriptionName("name"), new ChannelName("name"), new RoutingKey("name"),
            bufferSize:1, noOfPerformers:1, messagePumpType: MessagePumpType.Proactor, timeOut: TimeSpan.FromMilliseconds(399));
            
 var exception = Assert.ThrowsExactly<ArgumentException>(() => factory.CreateAsyncChannel(subscription));

        await Assert.That(exception.Message).IsEqualTo("The minimum allowed timeout is 400 milliseconds");
    }
        
    [Test]
    public async Task When_the_timeout_is_below_400_ms_it_should_throw_an_exception_async_channel_async()
    {
        var factory = new AzureServiceBusChannelFactory(new AzureServiceBusConsumerFactory(new AzureServiceBusConfiguration("Endpoint=sb://someString.servicebus.windows.net;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=oUWJw7777s7ydjdafqFqhk9O7TOs=")));

        var subscription = new AzureServiceBusSubscription<ASBTestCommand>(new SubscriptionName("name"), new ChannelName("name"), new RoutingKey("name"),
            bufferSize:1, noOfPerformers:1, messagePumpType: MessagePumpType.Proactor, timeOut: TimeSpan.FromMilliseconds(399));

 var exception = await Assert.ThrowsAsync<ArgumentException>(() => factory.CreateAsyncChannelAsync(subscription));

        await Assert.That(exception.Message).IsEqualTo("The minimum allowed timeout is 400 milliseconds");
    }
}
