using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.Testing;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor;

public class MessageDispatchRejectMessageExceptionTestsAsync
{
    private readonly RoutingKey _routingKey = new("myTopic");
    private readonly RoutingKey _deadLetterRoutingKey = new("myDeadLetterRoutingKey");
    private readonly InternalBus _bus = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private Dispatcher _dispatcher;

    public MessageDispatchRejectMessageExceptionTestsAsync()
    {
        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.RegisterAsync<MyRejectedEvent, MyRejectedEventHandlerAsync>();
        
        var handlerFactory = new SimpleHandlerFactory( 
            (type) => throw new ConfigurationException(),
            (type) =>  new MyRejectedEventHandlerAsync()
        );

        var mapperFactory = new SimpleMessageMapperFactoryAsync((r) => new MyRejectedEventHandlerMessageMapperAsync());
        var messageMapperRegistryAsync = new MessageMapperRegistry(null, mapperFactory);
        messageMapperRegistryAsync.RegisterAsync<MyRejectedEvent, MyRejectedEventHandlerMessageMapperAsync>();
            

        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>();
        resiliencePipelineRegistry.AddBrighterDefault();
        
        var commandProcessor = new CommandProcessor(
            subscriberRegistry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            new PolicyRegistry(),
            resiliencePipelineRegistry,
            new InMemorySchedulerFactory()
        );
        
        var subscription = new InMemorySubscription<MyRejectedEvent>(
            new SubscriptionName("test"), 
            noOfPerformers: 1, 
            timeOut: TimeSpan.FromMilliseconds(1000), 
            channelFactory: new InMemoryChannelFactory(_bus, _timeProvider),
            channelName: new ChannelName("myChannel"), 
            messagePumpType: MessagePumpType.Proactor,
            routingKey: _routingKey
        );

        subscription.DeadLetterRoutingKey = _deadLetterRoutingKey;
        
        _dispatcher = new Dispatcher(
            commandProcessor, 
            new List<Subscription> { subscription },
            null,
            messageMapperRegistryAsync,
            requestContextFactory: new InMemoryRequestContextFactory()
        );
        
        var @event = new MyRejectedEvent(Id.Random());
        var message = new MyRejectedEventHandlerMessageMapperAsync()
            .MapToMessageAsync(@event, new Publication{Topic = _routingKey})
            .Result;
        _bus.Enqueue(message);
        
        _dispatcher.Receive(); 
    }

    [Fact]
    public async Task When_an_event_handler_throw_a_reject_message_exception()
    {
        // Allow time for async message processing to complete before stopping
        await Task.Delay(1000); // slopwatch:suppress SW004 - message pump runs on background thread with no sync point

        await _dispatcher.End();
        
        Assert.Empty(_bus.Stream(_routingKey));
        Assert.NotEmpty(_bus.Stream(_deadLetterRoutingKey));
        var message = _bus.Dequeue(_deadLetterRoutingKey);
        var rejectionReason = $"Message rejected reason: {RejectionReason.DeliveryError} Description: {MyRejectedEventHandlerAsync.TestOfRejectionFlow}";
        Assert.Equal(rejectionReason, message.Header.Bag[Message.RejectionReasonHeaderName]);
    }
    
    
}
