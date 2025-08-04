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
       Assert.True(_requestContextFactory.CreateWasCalled);
       Assert.Equal(_requestContextFactory.Context.Bag["TestString"].ToString(), MyContextAwareCommandHandler.TestString);
       Assert.Equal("I was called and set the context", _requestContextFactory.Context.Bag["MyContextAwareCommandHandler"]);
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
        Assert.True(_requestContextFactory.CreateWasCalled);
        Assert.Equal(_requestContextFactory.Context.Bag["TestString"].ToString(), MyContextAwareCommandHandlerAsync.TestString);
        Assert.Equal("I was called and set the context", _requestContextFactory.Context.Bag["MyContextAwareCommandHandler"]);
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
        Assert.True(_requestContextFactory.CreateWasCalled);
        Assert.Equal(_requestContextFactory.Context.Bag["TestString"].ToString(), MyContextAwareEventHandler.TestString);
        Assert.Equal("I was called and set the context", _requestContextFactory.Context.Bag["MyContextAwareEventHandler"]);
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
        Assert.True(_requestContextFactory.CreateWasCalled);
        Assert.Equal(_requestContextFactory.Context.Bag["TestString"].ToString(), MyContextAwareEventHandlerAsync.TestString);
        Assert.Equal("I was called and set the context", _requestContextFactory.Context.Bag["MyContextAwareEventHandler"]);
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
                {
                    routingKey, new InMemoryMessageProducer(new InternalBus(), timeProvider, new Publication{RequestType = typeof(MyCommand), Topic = routingKey})
                }
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
            new FindPublicationByPublicationTopicOrRequestType(),
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
        Assert.True(_requestContextFactory.CreateWasCalled);
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
                { 
                    routingKey, new InMemoryMessageProducer(new InternalBus(), timeProvider, new Publication{RequestType = typeof(MyCommand), Topic = routingKey})
                },
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
            new FindPublicationByPublicationTopicOrRequestType(),
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
        Assert.True(_requestContextFactory.CreateWasCalled);
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
                { routingKey, new InMemoryMessageProducer(new InternalBus(), timeProvider, instrumentationOptions:InstrumentationOptions.All)
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
            new FindPublicationByPublicationTopicOrRequestType(),
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
        Assert.True(_requestContextFactory.CreateWasCalled);
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
                { routingKey, new InMemoryMessageProducer(new InternalBus(), timeProvider, instrumentationOptions:InstrumentationOptions.All)
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
            new FindPublicationByPublicationTopicOrRequestType(),
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
        Assert.True(_requestContextFactory.CreateWasCalled);

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
