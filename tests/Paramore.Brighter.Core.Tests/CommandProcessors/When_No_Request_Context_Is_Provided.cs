using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.CommandProcessors;
public class RequestContextFromFactoryTests
{
    private readonly SpyContextFactory _requestContextFactory;
    private readonly IPolicyRegistry<string> _policyRegistry;
    private readonly ResiliencePipelineRegistry<string> _resiliencePipelineRegistry;
    public RequestContextFromFactoryTests()
    {
        _policyRegistry = new DefaultPolicy();
        _resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
        _requestContextFactory = new SpyContextFactory
        {
            Context = null,
            CreateWasCalled = false
        };
    }

    [Test]
    public async Task When_No_Request_Context_Is_Provided_On_A_Send()
    {
        //arrange
        var registry = new SubscriberRegistry();
        registry.Register<MyCommand, MyContextAwareCommandHandler>();
        var contextCapture = new ContextCapture();
        var handlerFactory = new SimpleHandlerFactorySync(_ => new MyContextAwareCommandHandler(contextCapture));
        var myCommand = new MyCommand();
        var commandProcessor = new CommandProcessor(registry, handlerFactory, _requestContextFactory, new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        //act
        commandProcessor.Send(myCommand);
        //assert
        await Assert.That(_requestContextFactory.CreateWasCalled).IsTrue();
        await Assert.That(contextCapture.TestString).IsEqualTo(_requestContextFactory.Context!.Bag["TestString"].ToString());
        await Assert.That(_requestContextFactory.Context.Bag["MyContextAwareCommandHandler"]).IsEqualTo("I was called and set the context");
    }

    [Test]
    public async Task When_No_Request_Context_Is_Provided_On_A_Send_Async()
    {
        //arrange
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyCommand, MyContextAwareCommandHandlerAsync>();
        var contextCapture = new ContextCapture();
        var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyContextAwareCommandHandlerAsync(contextCapture));
        var myCommand = new MyCommand();
        var commandProcessor = new CommandProcessor(registry, handlerFactory, _requestContextFactory, new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        //act
        await commandProcessor.SendAsync(myCommand);
        //assert
        await Assert.That(_requestContextFactory.CreateWasCalled).IsTrue();
        await Assert.That(contextCapture.TestString).IsEqualTo(_requestContextFactory.Context!.Bag["TestString"].ToString());
        await Assert.That(_requestContextFactory.Context.Bag["MyContextAwareCommandHandler"]).IsEqualTo("I was called and set the context");
    }

    [Test]
    public async Task When_No_Request_Context_Is_Provided_On_A_Publish()
    {
        //arrange
        var registry = new SubscriberRegistry();
        registry.Register<MyEvent, MyContextAwareEventHandler>();
        var contextCapture = new ContextCapture();
        var handlerFactory = new SimpleHandlerFactorySync(_ => new MyContextAwareEventHandler(contextCapture));
        var myEvent = new MyEvent();
        var commandProcessor = new CommandProcessor(registry, handlerFactory, _requestContextFactory, new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        //act
        commandProcessor.Publish(myEvent);
        //assert
        await Assert.That(_requestContextFactory.CreateWasCalled).IsTrue();
        await Assert.That(contextCapture.TestString).IsEqualTo(_requestContextFactory.Context.Bag["TestString"].ToString());
        await Assert.That(_requestContextFactory.Context.Bag["MyContextAwareEventHandler"]).IsEqualTo("I was called and set the context");
    }

    [Test]
    public async Task When_No_Request_Context_Is_Provided_On_A_Publish_Async()
    {
        //arrange
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<MyEvent, MyContextAwareEventHandlerAsync>();
        var contextCapture = new ContextCapture();
        var handlerFactory = new SimpleHandlerFactoryAsync(_ => new MyContextAwareEventHandlerAsync(contextCapture));
        var myEvent = new MyEvent();
        var commandProcessor = new CommandProcessor(registry, handlerFactory, _requestContextFactory, new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        //act
        await commandProcessor.PublishAsync(myEvent);
        //assert
        await Assert.That(_requestContextFactory.CreateWasCalled).IsTrue();
        await Assert.That(contextCapture.TestString).IsEqualTo(_requestContextFactory.Context.Bag["TestString"].ToString());
        await Assert.That(_requestContextFactory.Context.Bag["MyContextAwareEventHandler"]).IsEqualTo("I was called and set the context");
    }

    [Test]
    public async Task When_No_Request_Context_Is_Provided_On_A_Deposit()
    {
        var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()), null);
        messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
        var timeProvider = new FakeTimeProvider();
        var routingKey = new RoutingKey("MyCommand");
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, new InMemoryMessageProducer(new InternalBus(), new Publication { RequestType = typeof(MyCommand), Topic = routingKey }) } });
        var tracer = new BrighterTracer();
        var fakeOutbox = new InMemoryOutbox(timeProvider)
        {
            Tracer = tracer
        };
        var bus = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry, _resiliencePipelineRegistry, messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), fakeOutbox);
        var commandProcessor = new CommandProcessor(_requestContextFactory, _policyRegistry, new ResiliencePipelineRegistry<string>(), bus, new InMemorySchedulerFactory());
        //act
        commandProcessor.DepositPost(new MyCommand());
        //assert
        await Assert.That(_requestContextFactory.CreateWasCalled).IsTrue();
    }

    [Test]
    public async Task When_No_Request_Context_Is_Provided_On_A_Deposit_Async()
    {
        var messageMapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync((_) => new MyCommandMessageMapperAsync()));
        messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();
        var timeProvider = new FakeTimeProvider();
        var routingKey = new RoutingKey("MyCommand");
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, new InMemoryMessageProducer(new InternalBus(), new Publication { RequestType = typeof(MyCommand), Topic = routingKey }) }, });
        var tracer = new BrighterTracer();
        var fakeOutbox = new InMemoryOutbox(timeProvider)
        {
            Tracer = tracer
        };
        var bus = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry, _resiliencePipelineRegistry, messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), fakeOutbox);
        var commandProcessor = new CommandProcessor(_requestContextFactory, _policyRegistry, new ResiliencePipelineRegistry<string>(), bus, new InMemorySchedulerFactory());
        //act
        await commandProcessor.DepositPostAsync(new MyCommand());
        //assert
        await Assert.That(_requestContextFactory.CreateWasCalled).IsTrue();
    }

    [Test]
    public async Task When_No_Request_Context_Is_Provided_On_A_Clear()
    {
        //arrange
        var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()), null);
        messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
        var timeProvider = new FakeTimeProvider();
        var routingKey = new RoutingKey("MyCommand");
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, new InMemoryMessageProducer(new InternalBus(), instrumentationOptions: InstrumentationOptions.All) { Publication = new Publication { RequestType = typeof(MyCommand), Topic = routingKey } } }, });
        var tracer = new BrighterTracer();
        var fakeOutbox = new InMemoryOutbox(timeProvider)
        {
            Tracer = tracer
        };
        var bus = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry, _resiliencePipelineRegistry, messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), fakeOutbox);
        var commandProcessor = new CommandProcessor(_requestContextFactory, _policyRegistry, new ResiliencePipelineRegistry<string>(), bus, new InMemorySchedulerFactory());
        var myCommand = new MyCommand()
        {
            Id = Guid.NewGuid().ToString()
        };
        var message = new Message(new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        bus.AddToOutbox(message, new RequestContext());
        //act
        commandProcessor.ClearOutbox(new[] { myCommand.Id });
        //assert
        await Assert.That(_requestContextFactory.CreateWasCalled).IsTrue();
    }

    [Test]
    public async Task When_A_Request_Context_Is_Provided_On_A_Clear_Async()
    {
        //arrange
        var messageMapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync((_) => new MyCommandMessageMapperAsync()));
        messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();
        var timeProvider = new FakeTimeProvider();
        var routingKey = new RoutingKey("MyCommand");
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, new InMemoryMessageProducer(new InternalBus(), instrumentationOptions: InstrumentationOptions.All) { Publication = new Publication { RequestType = typeof(MyCommand), Topic = routingKey } } }, });
        var tracer = new BrighterTracer();
        var fakeOutbox = new InMemoryOutbox(timeProvider)
        {
            Tracer = tracer
        };
        var bus = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry, _resiliencePipelineRegistry, messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), fakeOutbox);
        var commandProcessor = new CommandProcessor(_requestContextFactory, _policyRegistry, new ResiliencePipelineRegistry<string>(), bus, new InMemorySchedulerFactory());
        var myCommand = new MyCommand()
        {
            Id = Guid.NewGuid().ToString()
        };
        var message = new Message(new MessageHeader(myCommand.Id, routingKey, MessageType.MT_COMMAND), new MessageBody("test content"));
        bus.AddToOutbox(message, new RequestContext());
        //act
        await commandProcessor.ClearOutboxAsync(new[] { myCommand.Id });
        //assert
        await Assert.That(_requestContextFactory.CreateWasCalled).IsTrue();
    }
}
