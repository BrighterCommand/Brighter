using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors;

[Collection("CommandProcessor")]
public class RequestContextFromFactoryTests : IDisposable 
{
    private readonly SpyContextFactory _requestContextFactory;
    private readonly IPolicyRegistry<string> _policyRegistry;

    public RequestContextFromFactoryTests()
    {
        MyContextAwareCommandHandler.TestString = null; 
        MyContextAwareCommandHandlerAsync.TestString = null;
        MyContextAwareEventHandler.TestString = null;
        MyContextAwareEventHandlerAsync.TestString = null;
        
        _policyRegistry = new DefaultPolicy();
        _requestContextFactory = new SpyContextFactory();
        _requestContextFactory.Context = null;
        _requestContextFactory.CreateWasCalled = false;
    }
    
    [Fact]
    public void When_No_Request_Context_Is_Provided_On_A_Send()
    {
       //arrange
       var registry = new SubscriberRegistry();
       registry.Register<MyCommand, MyContextAwareCommandHandler>();
       var handlerFactory = new SimpleHandlerFactorySync(_ => new MyContextAwareCommandHandler());
       var myCommand = new MyCommand();

       var commandProcessor = new CommandProcessor(registry, handlerFactory, _requestContextFactory, new PolicyRegistry(), new InMemorySchedulerFactory());

       //act
       commandProcessor.Send(myCommand);
       
       //assert
       _requestContextFactory.CreateWasCalled.Should().BeTrue();
       //_should_have_seen_the_data_we_pushed_into_the_bag  dd
       MyContextAwareCommandHandler.TestString.Should().Be(_requestContextFactory.Context.Bag["TestString"].ToString());
       //_should_have_been_filled_by_the_handler
       _requestContextFactory.Context.Bag["MyContextAwareCommandHandler"].Should().Be("I was called and set the context");
    }
    
    [Fact]
    public async Task When_No_Request_Context_Is_Provided_On_A_Send_Async()
    {
        //arrange
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyContextAwareCommandHandlerAsync>();
        var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyContextAwareCommandHandlerAsync());
        var myCommand = new MyCommand();

        var commandProcessor = new CommandProcessor(registry, handlerFactory, _requestContextFactory, new PolicyRegistry(), new InMemorySchedulerFactory());

        //act
        await commandProcessor.SendAsync(myCommand);
       
        //assert
        _requestContextFactory.CreateWasCalled.Should().BeTrue();
        //_should_have_seen_the_data_we_pushed_into_the_bag  dd
        MyContextAwareCommandHandlerAsync.TestString.Should().Be(_requestContextFactory.Context.Bag["TestString"].ToString());
        //_should_have_been_filled_by_the_handler
        _requestContextFactory.Context.Bag["MyContextAwareCommandHandler"].Should().Be("I was called and set the context");
    }
    
    [Fact]
    public void When_No_Request_Context_Is_Provided_On_A_Publish()
    {
        //arrange
        var registry = new SubscriberRegistry();
        registry.Register<MyEvent, MyContextAwareEventHandler>();
        var handlerFactory = new SimpleHandlerFactorySync(_ => new MyContextAwareEventHandler());
        var myEvent = new MyEvent();

        var commandProcessor = new CommandProcessor(registry, handlerFactory, _requestContextFactory, new PolicyRegistry(), new InMemorySchedulerFactory());

        //act
        commandProcessor.Publish(myEvent);
       
        //assert
        _requestContextFactory.CreateWasCalled.Should().BeTrue();
        //_should_have_seen_the_data_we_pushed_into_the_bag  dd
        MyContextAwareEventHandler.TestString.Should().Be(_requestContextFactory.Context.Bag["TestString"].ToString());
        //_should_have_been_filled_by_the_handler
        _requestContextFactory.Context.Bag["MyContextAwareEventHandler"].Should().Be("I was called and set the context");
    }
    
    [Fact]
    public async Task When_No_Request_Context_Is_Provided_On_A_Publish_Async()
    {
        //arrange
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyEvent, MyContextAwareEventHandlerAsync>();
        var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyContextAwareEventHandlerAsync());
        var myEvent = new MyEvent();

        var commandProcessor = new CommandProcessor(registry, handlerFactory, _requestContextFactory, new PolicyRegistry(), new InMemorySchedulerFactory());

        //act
        await commandProcessor.PublishAsync(myEvent);
       
        //assert
        _requestContextFactory.CreateWasCalled.Should().BeTrue();
        //_should_have_seen_the_data_we_pushed_into_the_bag  dd
        MyContextAwareEventHandlerAsync.TestString.Should().Be(_requestContextFactory.Context.Bag["TestString"].ToString());
        //_should_have_been_filled_by_the_handler
        _requestContextFactory.Context.Bag["MyContextAwareEventHandler"].Should().Be("I was called and set the context");
    }

    [Fact]
    public void When_No_Request_Context_Is_Provided_On_A_Deposit()
    {
        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
            null);
        messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

        var timeProvider = new FakeTimeProvider();
        var routingKey = new RoutingKey("MyCommand");
        
        var producerRegistry =
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { routingKey, new InMemoryProducer(new InternalBus(), timeProvider)
                {
                    Publication = new Publication{RequestType = typeof(MyCommand), Topic = routingKey}
                } },
            });

        var tracer = new BrighterTracer();
        var fakeOutbox = new InMemoryOutbox(timeProvider) {Tracer = tracer};
        
        var bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry, 
            _policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            fakeOutbox
        );
        
        var commandProcessor = new CommandProcessor(
            _requestContextFactory,
            _policyRegistry,
            bus,
            new InMemorySchedulerFactory()
        );

        //act
        commandProcessor.DepositPost(new MyCommand());

        //assert
        _requestContextFactory.CreateWasCalled.Should().BeTrue();
    } 
    
    [Fact]
    public async Task When_No_Request_Context_Is_Provided_On_A_Deposit_Async()
    {
        var messageMapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync((_) => new MyCommandMessageMapperAsync()));
        messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();

        var timeProvider = new FakeTimeProvider();
        var routingKey = new RoutingKey("MyCommand");
        
        var producerRegistry =
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { routingKey, new InMemoryProducer(new InternalBus(), timeProvider)
                {
                    Publication = new Publication{RequestType = typeof(MyCommand), Topic = routingKey}
                } },
            });
            
        var tracer = new BrighterTracer();
        var fakeOutbox = new InMemoryOutbox(timeProvider) {Tracer = tracer};
        
        var bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry, 
            _policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            fakeOutbox
        );
        
        var commandProcessor = new CommandProcessor(
            _requestContextFactory,
            _policyRegistry,
            bus,
            new InMemorySchedulerFactory()
        );

        //act
        await commandProcessor.DepositPostAsync(new MyCommand());

        //assert
        _requestContextFactory.CreateWasCalled.Should().BeTrue();
    } 
    
    [Fact]
    public void When_No_Request_Context_Is_Provided_On_A_Clear()
    {
        //arrange
        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
            null);
        messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

        var timeProvider = new FakeTimeProvider();
        var routingKey = new RoutingKey("MyCommand");
        
        var producerRegistry =
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { routingKey, new InMemoryProducer(new InternalBus(), timeProvider)
                {
                    Publication = new Publication{RequestType = typeof(MyCommand), Topic = routingKey}
                } },
            });
            
        var tracer = new BrighterTracer();
        var fakeOutbox = new InMemoryOutbox(timeProvider) {Tracer = tracer};
        
        var bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry, 
            _policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            fakeOutbox
        );
        
        var commandProcessor = new CommandProcessor(
            _requestContextFactory,
            _policyRegistry,
            bus,
            new InMemorySchedulerFactory()
        );
        
        var myCommand = new MyCommand() {Id = Guid.NewGuid().ToString()};
        var message = new Message(new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        bus.AddToOutbox(message, new RequestContext());
            
        //act
        commandProcessor.ClearOutbox(new []{myCommand.Id});

        //assert
        _requestContextFactory.CreateWasCalled.Should().BeTrue();
    }
    
    [Fact]
    public async Task When_A_Request_Context_Is_Provided_On_A_Clear_Async()
    {
        //arrange
        var messageMapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync((_) => new MyCommandMessageMapperAsync()));
        messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();

        var timeProvider = new FakeTimeProvider();
        var routingKey = new RoutingKey("MyCommand");
        
        var producerRegistry =
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { routingKey, new InMemoryProducer(new InternalBus(), timeProvider)
                {
                    Publication = new Publication{RequestType = typeof(MyCommand), Topic = routingKey}
                } },
            });
            
        var tracer = new BrighterTracer();
        var fakeOutbox = new InMemoryOutbox(timeProvider) {Tracer = tracer};
        
        var bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry, 
            _policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            fakeOutbox
        );
        
        var commandProcessor = new CommandProcessor(
            _requestContextFactory,
            _policyRegistry,
            bus,
            new InMemorySchedulerFactory()
        );
        
        var myCommand = new MyCommand() {Id = Guid.NewGuid().ToString()};
        var message = new Message(new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        bus.AddToOutbox(message, new RequestContext());
            
        //act
        await commandProcessor.ClearOutboxAsync(new []{myCommand.Id});

        //assert
        _requestContextFactory.CreateWasCalled.Should().BeTrue();

    }
    
    public void Dispose()
    {
        MyContextAwareCommandHandler.TestString = null; 
        MyContextAwareCommandHandlerAsync.TestString = null;
        MyContextAwareEventHandler.TestString = null;
        MyContextAwareEventHandlerAsync.TestString = null;
        CommandProcessor.ClearServiceBus();
    }
}
