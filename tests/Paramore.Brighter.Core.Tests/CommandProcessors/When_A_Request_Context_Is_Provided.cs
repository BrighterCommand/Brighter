using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors;

[Collection("CommandProcessor")]
public class RequestContextPresentTests : IDisposable 
{
    private readonly SpyContextFactory _requestContextFactory;
    private readonly IPolicyRegistry<string> _policyRegistry;

    public RequestContextPresentTests()
    {
       MyContextAwareCommandHandler.TestString = null; 
       MyContextAwareCommandHandlerAsync.TestString = null;
       MyContextAwareEventHandler.TestString = null;
       MyContextAwareEventHandlerAsync.TestString = null;
       
        _policyRegistry = new DefaultPolicy();
        _requestContextFactory = new SpyContextFactory();
        _requestContextFactory.CreateWasCalled = false;
    }

    [Fact]
    public void When_A_Request_Context_Is_Provided_On_A_Send()
    {
        //arrange
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyContextAwareCommandHandler>();
        var handlerFactory = new TestHandlerFactorySync<MyCommand, MyContextAwareCommandHandler>(() => new MyContextAwareCommandHandler());
        var spyRequestContextFactory = new SpyContextFactory();
        var policyRegistry = new DefaultPolicy();

        var commandProcessor = new CommandProcessor(
            registry,
            handlerFactory,
            spyRequestContextFactory,
            policyRegistry
        );

        //act
        var context = new RequestContext();
        var testBagValue = Guid.NewGuid().ToString();
        context.Bag.Add("TestString", testBagValue) ;
        commandProcessor.Send(new MyCommand(), context);

        //assert
        spyRequestContextFactory.CreateWasCalled.Should().BeFalse();
        MyContextAwareCommandHandler.TestString.Should().Be(testBagValue);
        context.Bag.Should().ContainKey("MyContextAwareCommandHandler");
        context.Bag["MyContextAwareCommandHandler"].Should().Be("I was called and set the context");
    }
    
    [Fact]
    public async Task When_A_Request_Context_Is_Provided_On_A_Send_Async()
    {
        //arrange
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyContextAwareCommandHandlerAsync>();
        var handlerFactory = new TestHandlerFactoryAsync<MyCommand, MyContextAwareCommandHandlerAsync>(() => new MyContextAwareCommandHandlerAsync());
        var spyRequestContextFactory = new SpyContextFactory();
        var policyRegistry = new DefaultPolicy();

        var commandProcessor = new CommandProcessor(
            registry,
            handlerFactory,
            spyRequestContextFactory,
            policyRegistry
        );

        //act
        var context = new RequestContext();
        var testBagValue = Guid.NewGuid().ToString();
        context.Bag.Add("TestString", testBagValue) ;
        await commandProcessor.SendAsync(new MyCommand(), context);

        //assert
        spyRequestContextFactory.CreateWasCalled.Should().BeFalse();
        MyContextAwareCommandHandlerAsync.TestString.Should().Be(testBagValue);
        context.Bag.Should().ContainKey("MyContextAwareCommandHandler");
        context.Bag["MyContextAwareCommandHandler"].Should().Be("I was called and set the context");
    }
    
    [Fact]
    public void When_A_Request_Context_Is_Provided_On_A_Publish()
    {
        //arrange
        var registry = new SubscriberRegistry();
        registry.Register<MyEvent, MyContextAwareEventHandler>();
        var handlerFactory = new TestHandlerFactorySync<MyEvent, MyContextAwareEventHandler>(() => new MyContextAwareEventHandler());

        var commandProcessor = new CommandProcessor(
            registry,
            handlerFactory,
            _requestContextFactory,
            _policyRegistry
        );

        //act
        var context = new RequestContext();
        var testBagValue = Guid.NewGuid().ToString();
        context.Bag.Add("TestString", testBagValue) ;
        commandProcessor.Publish(new MyEvent(), context);

        //assert
        _requestContextFactory.CreateWasCalled.Should().BeFalse();
        MyContextAwareEventHandler.TestString.Should().Be(testBagValue);
        context.Bag.Should().ContainKey("MyContextAwareEventHandler");
        context.Bag["MyContextAwareEventHandler"].Should().Be("I was called and set the context");
    }
    
    [Fact]
    public async Task When_A_Request_Context_Is_Provided_On_A_Publish_Async()
    {
        //arrange
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyEvent, MyContextAwareEventHandlerAsync>();
        var handlerFactory = new TestHandlerFactoryAsync<MyEvent, MyContextAwareEventHandlerAsync>(() => new MyContextAwareEventHandlerAsync());

        var commandProcessor = new CommandProcessor(
            registry,
            handlerFactory,
            _requestContextFactory,
            _policyRegistry
        );

        //act
        var context = new RequestContext();
        var testBagValue = Guid.NewGuid().ToString();
        context.Bag.Add("TestString", testBagValue) ;
        await commandProcessor.PublishAsync(new MyEvent(), context);

        //assert
        _requestContextFactory.CreateWasCalled.Should().BeFalse();
        MyContextAwareEventHandlerAsync.TestString.Should().Be(testBagValue);
        context.Bag.Should().ContainKey("MyContextAwareEventHandler");
        context.Bag["MyContextAwareEventHandler"].Should().Be("I was called and set the context");
    }
    
    [Fact]
    public void When_A_Request_Context_Is_Provided_On_A_Deposit()
    {
        //arrange
        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
            null);
        messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

        var producerRegistry =
            new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
            {
                { "MyCommand", new FakeMessageProducer
                {
                    Publication = new Publication{RequestType = typeof(MyCommand), Topic = new RoutingKey("MyCommand")}
                } },
            });
            
        var fakeOutbox = new FakeOutbox();
        
        var bus = new ExternalBusService<Message, CommittableTransaction>(
            producerRegistry, 
            _policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            _requestContextFactory,
            fakeOutbox
        );
        
        var commandProcessor = new CommandProcessor(
            _requestContextFactory,
            _policyRegistry,
            bus
        );

        //act
        var context = new RequestContext();
        var testBagValue = Guid.NewGuid().ToString();
        context.Bag.Add("TestString", testBagValue) ;
        commandProcessor.DepositPost(new MyCommand(), context);

        //assert
        _requestContextFactory.CreateWasCalled.Should().BeFalse();
    } 
    
    [Fact]
    public async Task When_A_Request_Context_Is_Provided_On_A_Deposit_Async()
    {
        //arrange
        var messageMapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync((_) => new MyCommandMessageMapperAsync()));
        messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();

        var producerRegistry =
            new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
            {
                { "MyCommand", new FakeMessageProducer
                {
                    Publication = new Publication{RequestType = typeof(MyCommand), Topic = new RoutingKey("MyCommand")}
                } },
            });
            
        var fakeOutbox = new FakeOutbox();
        
        var bus = new ExternalBusService<Message, CommittableTransaction>(
            producerRegistry, 
            _policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            _requestContextFactory,
            fakeOutbox
        );
        
        var commandProcessor = new CommandProcessor(
            _requestContextFactory,
            _policyRegistry,
            bus
        );

        //act
        var context = new RequestContext();
        var testBagValue = Guid.NewGuid().ToString();
        context.Bag.Add("TestString", testBagValue) ;
        await commandProcessor.DepositPostAsync(new MyCommand(), context);

        //assert
        _requestContextFactory.CreateWasCalled.Should().BeFalse();
    } 
    
    [Fact]
    public void When_A_Request_Context_Is_Provided_On_A_Clear()
    {
        //arrange
        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
            null);
        messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

        var producerRegistry =
            new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
            {
                { "MyCommand", new FakeMessageProducer
                {
                    Publication = new Publication{RequestType = typeof(MyCommand), Topic = new RoutingKey("MyCommand")}
                } },
            });
            
        var fakeOutbox = new FakeOutbox();
        
        var bus = new ExternalBusService<Message, CommittableTransaction>(
            producerRegistry, 
            _policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            _requestContextFactory,
            fakeOutbox
        );
        
        var commandProcessor = new CommandProcessor(
            _requestContextFactory,
            _policyRegistry,
            bus
        );
        
        var myCommand = new MyCommand() {Id = Guid.NewGuid().ToString()};
        var message = new Message(new MessageHeader(myCommand.Id, "MyCommand", MessageType.MT_COMMAND), new MessageBody("test content"));
        bus.AddToOutbox(message, new RequestContext());
            
        //act
        var context = new RequestContext();
        var testBagValue = Guid.NewGuid().ToString();
        context.Bag.Add("TestString", testBagValue) ;
        commandProcessor.ClearOutbox(new []{myCommand.Id}, context);

        //assert
        _requestContextFactory.CreateWasCalled.Should().BeFalse();
    }
    
    [Fact]
    public async Task When_A_Request_Context_Is_Provided_On_A_Clear_Async()
    {
        //arrange
        var messageMapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync((_) => new MyCommandMessageMapperAsync()));
        messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();
        
        var producerRegistry =
            new ProducerRegistry(new Dictionary<string, IAmAMessageProducer>
            {
                { "MyCommand", new FakeMessageProducer
                {
                    Publication = new Publication{RequestType = typeof(MyCommand), Topic = new RoutingKey("MyCommand")}
                } },
            });
            
        var fakeOutbox = new FakeOutbox();
        
        var bus = new ExternalBusService<Message, CommittableTransaction>(
            producerRegistry, 
            _policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            _requestContextFactory,
            fakeOutbox
        );
        
        var commandProcessor = new CommandProcessor(
            _requestContextFactory,
            _policyRegistry,
            bus
        );
        
        var myCommand = new MyCommand() {Id = Guid.NewGuid().ToString()};
        var message = new Message(new MessageHeader(myCommand.Id, "MyCommand", MessageType.MT_COMMAND), new MessageBody("test content"));
        bus.AddToOutbox(message, new RequestContext());
            
        //act
        var context = new RequestContext();
        var testBagValue = Guid.NewGuid().ToString();
        context.Bag.Add("TestString", testBagValue) ;
        await commandProcessor.ClearOutboxAsync(new []{myCommand.Id}, context);

        //assert
        _requestContextFactory.CreateWasCalled.Should().BeFalse();

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
