using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors;

[Collection("CommandProcessor")]
public class RequestContextPresentTests : IDisposable
{
    private readonly SpyContextFactory _requestContextFactory;
    private readonly IPolicyRegistry<string> _policyRegistry;
    private readonly ResiliencePipelineRegistry<string>  _resiliencePipelineRegistry;

    public RequestContextPresentTests()
    {
       MyContextAwareCommandHandler.TestString = null;
       MyContextAwareCommandHandlerAsync.TestString = null;
       MyContextAwareEventHandler.TestString = null;
       MyContextAwareEventHandlerAsync.TestString = null;

        _policyRegistry = new DefaultPolicy();
        _resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
        _requestContextFactory = new SpyContextFactory { CreateWasCalled = false };
    }

    [Fact]
    public void When_A_Request_Context_Is_Provided_On_A_Send()
    {
        //arrange
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyContextAwareCommandHandler>();
        var handlerFactory = new SimpleHandlerFactorySync(_ => new MyContextAwareCommandHandler());
        var spyRequestContextFactory = new SpyContextFactory();
        var policyRegistry = new DefaultPolicy();

        var commandProcessor = new CommandProcessor(
            registry,
            handlerFactory,
            spyRequestContextFactory,
            policyRegistry,
            new ResiliencePipelineRegistry<string>(),
            new InMemorySchedulerFactory()
        );

        //act
        var context = new RequestContext();
        var testBagValue = Guid.NewGuid().ToString();
        context.Bag.AddOrUpdate("TestString", testBagValue, (_, _) => testBagValue) ;
        commandProcessor.Send(new MyCommand(), context);

        //assert
        Assert.False(spyRequestContextFactory.CreateWasCalled);
        Assert.Equal(testBagValue, MyContextAwareCommandHandler.TestString);
        Assert.Contains("MyContextAwareCommandHandler", context.Bag);
        Assert.Equal("I was called and set the context", context.Bag["MyContextAwareCommandHandler"]);
    }

    [Fact]
    public async Task When_A_Request_Context_Is_Provided_On_A_Send_Async()
    {
        //arrange
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyContextAwareCommandHandlerAsync>();
        var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyContextAwareCommandHandlerAsync());
        var spyRequestContextFactory = new SpyContextFactory();
        var policyRegistry = new DefaultPolicy();

        var commandProcessor = new CommandProcessor(
            registry,
            handlerFactory,
            spyRequestContextFactory,
            policyRegistry,
            new ResiliencePipelineRegistry<string>(),
            new InMemorySchedulerFactory()
        );

        //act
        var context = new RequestContext();
        var testBagValue = Guid.NewGuid().ToString();
        context.Bag.AddOrUpdate("TestString", testBagValue, (_, _) => testBagValue);
        await commandProcessor.SendAsync(new MyCommand(), context);

        //assert
        Assert.False(spyRequestContextFactory.CreateWasCalled);
        Assert.Equal(testBagValue, MyContextAwareCommandHandlerAsync.TestString);
        Assert.Contains("MyContextAwareCommandHandler", context.Bag);
        Assert.Equal("I was called and set the context", context.Bag["MyContextAwareCommandHandler"]);
    }

    [Fact]
    public void When_A_Request_Context_Is_Provided_On_A_Publish()
    {
        //arrange
        var registry = new SubscriberRegistry();
        registry.Register<MyEvent, MyContextAwareEventHandler>();
        var handlerFactory = new SimpleHandlerFactorySync(_ => new MyContextAwareEventHandler());

        var commandProcessor = new CommandProcessor(
            registry,
            handlerFactory,
            _requestContextFactory,
            _policyRegistry,
            new ResiliencePipelineRegistry<string>(),
            new InMemorySchedulerFactory()
        );

        //act
        var context = new RequestContext();
        var testBagValue = Guid.NewGuid().ToString();
        context.Bag.AddOrUpdate("TestString", testBagValue, (_, _) => testBagValue);
        commandProcessor.Publish(new MyEvent(), context);

        //assert
        Assert.False(_requestContextFactory.CreateWasCalled);
        Assert.Equal(testBagValue, MyContextAwareEventHandler.TestString);
        Assert.Contains("MyContextAwareEventHandler", context.Bag);
        Assert.Equal("I was called and set the context", context.Bag["MyContextAwareEventHandler"]);
    }

    [Fact]
    public async Task When_A_Request_Context_Is_Provided_On_A_Publish_Async()
    {
        //arrange
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyEvent, MyContextAwareEventHandlerAsync>();
        var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyContextAwareEventHandlerAsync());

        var commandProcessor = new CommandProcessor(
            registry,
            handlerFactory,
            _requestContextFactory,
            _policyRegistry,
            new ResiliencePipelineRegistry<string>(),
            new InMemorySchedulerFactory()
        );

        //act
        var context = new RequestContext();
        var testBagValue = Guid.NewGuid().ToString();
        context.Bag.AddOrUpdate("TestString", testBagValue, (_, _) => testBagValue);
        await commandProcessor.PublishAsync(new MyEvent(), context);

        //assert
        Assert.False(_requestContextFactory.CreateWasCalled);
        Assert.Equal(testBagValue, MyContextAwareEventHandlerAsync.TestString);
        Assert.Contains("MyContextAwareEventHandler", context.Bag);
        Assert.Equal("I was called and set the context", context.Bag["MyContextAwareEventHandler"]);
    }

    [Fact]
    public void When_A_Request_Context_Is_Provided_On_A_Deposit()
    {
        //arrange
        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
            null);
        messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

        var fakeTimeProvider = new FakeTimeProvider();
        var routingKey = new RoutingKey("MyCommand");

        var producerRegistry =
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { 
                    routingKey, new InMemoryMessageProducer(new InternalBus(), fakeTimeProvider,  new Publication{RequestType = typeof(MyCommand), Topic = routingKey})
                },
            });

        var timeProvider = new FakeTimeProvider();
        var tracer = new BrighterTracer(timeProvider);
        var fakeOutbox = new InMemoryOutbox(timeProvider) {Tracer = tracer};

        var bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            _resiliencePipelineRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            new FindPublicationByPublicationTopicOrRequestType(),
            fakeOutbox
        );

        var commandProcessor = new CommandProcessor(
            _requestContextFactory,
            _policyRegistry,
            new ResiliencePipelineRegistry<string>(),
            bus,
            new InMemorySchedulerFactory()
        );

        //act
        var context = new RequestContext();
        var testBagValue = Guid.NewGuid().ToString();
        context.Bag.AddOrUpdate("TestString", testBagValue, (_, _) => testBagValue) ;
        commandProcessor.DepositPost(new MyCommand(), context);

        //assert
        Assert.False(_requestContextFactory.CreateWasCalled);
    }

    [Fact]
    public async Task When_A_Request_Context_Is_Provided_On_A_Deposit_Async()
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
                { 
                    routingKey, new InMemoryMessageProducer(new InternalBus(), timeProvider, new Publication{RequestType = typeof(MyCommand), Topic = routingKey})
                 },
            });

        var tracer = new BrighterTracer(timeProvider);
        var fakeOutbox = new InMemoryOutbox(timeProvider) {Tracer = tracer};

        var bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            _resiliencePipelineRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            new FindPublicationByPublicationTopicOrRequestType(),
            fakeOutbox
        );

        var commandProcessor = new CommandProcessor(
            _requestContextFactory,
            _policyRegistry,
            new ResiliencePipelineRegistry<string>(),
            bus,
            new InMemorySchedulerFactory()
        );

        //act
        var context = new RequestContext();
        var testBagValue = Guid.NewGuid().ToString();
        context.Bag.AddOrUpdate("TestString", testBagValue, (_, _) => testBagValue);
        await commandProcessor.DepositPostAsync(new MyCommand(), context);

        //assert
        Assert.False(_requestContextFactory.CreateWasCalled);
    }

    [Fact]
    public void When_A_Request_Context_Is_Provided_On_A_Clear()
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
                { routingKey, new InMemoryMessageProducer(new InternalBus(), timeProvider, new Publication{RequestType = typeof(MyCommand), Topic = routingKey})} 
            });

        var tracer = new BrighterTracer(timeProvider);
        var fakeOutbox = new InMemoryOutbox(timeProvider) {Tracer = tracer};

        var bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            _resiliencePipelineRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            new FindPublicationByPublicationTopicOrRequestType(),
            fakeOutbox
        );

        var commandProcessor = new CommandProcessor(
            _requestContextFactory,
            _policyRegistry,
            new ResiliencePipelineRegistry<string>(),
            bus,
            new InMemorySchedulerFactory()
        );

        var myCommand = new MyCommand() {Id = Guid.NewGuid().ToString()};
        var message = new Message(new MessageHeader(myCommand.Id, new("MyCommand"), MessageType.MT_COMMAND), new MessageBody("test content"));
        bus.AddToOutbox(message, new RequestContext());

        //act
        var context = new RequestContext();
        var testBagValue = Guid.NewGuid().ToString();
        context.Bag.AddOrUpdate("TestString", testBagValue, (_, _) => testBagValue) ;
        commandProcessor.ClearOutbox(new []{myCommand.Id}, context);

        //assert
        Assert.False(_requestContextFactory.CreateWasCalled);
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
                { 
                    routingKey, new InMemoryMessageProducer(new InternalBus(), timeProvider,  new Publication{RequestType = typeof(MyCommand), Topic = routingKey} )
                 },
            });

        var tracer = new BrighterTracer(timeProvider);
        var fakeOutbox = new InMemoryOutbox(timeProvider);

        var bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            _resiliencePipelineRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            new FindPublicationByPublicationTopicOrRequestType(),
            fakeOutbox
        );

        var commandProcessor = new CommandProcessor(
            _requestContextFactory,
            _policyRegistry,
            new ResiliencePipelineRegistry<string>(),
            bus,
            new InMemorySchedulerFactory()
        );

        var myCommand = new MyCommand() {Id = Guid.NewGuid().ToString()};
        var message = new Message(new MessageHeader(myCommand.Id, new("MyCommand"), MessageType.MT_COMMAND), new MessageBody("test content"));
        bus.AddToOutbox(message, new RequestContext());

        //act
        var context = new RequestContext();
        var testBagValue = Guid.NewGuid().ToString();
        context.Bag.AddOrUpdate("TestString", testBagValue, (_, _) => testBagValue) ;
        await commandProcessor.ClearOutboxAsync([myCommand.Id], context);

        //assert
        Assert.False(_requestContextFactory.CreateWasCalled);

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
