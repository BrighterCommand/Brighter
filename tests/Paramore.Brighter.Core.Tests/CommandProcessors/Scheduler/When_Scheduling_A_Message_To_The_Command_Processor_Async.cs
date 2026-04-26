using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Polly;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Scheduler;
public class CommandProcessorSchedulerCommandAsyncTests
{
    private const string Topic = "MyCommand";
    private readonly CommandProcessor _commandProcessor;
    private readonly MyCommand _myCommand;
    private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
    private readonly FakeTimeProvider _timeProvider;
    private readonly InMemoryOutbox _outbox;
    private readonly InternalBus _internalBus = new();
    public CommandProcessorSchedulerCommandAsyncTests()
    {
        _myCommand = new()
        {
            Value = $"Hello World {Guid.NewGuid():N}"};
        var routingKey = new RoutingKey("MyCommand");
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(DateTimeOffset.UtcNow);
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<FireSchedulerRequest, FireSchedulerRequestHandler>();
        registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
        var handlerFactory = new SimpleHandlerFactoryAsync(type =>
        {
            if (type == typeof(FireSchedulerRequestHandler))
            {
                return new FireSchedulerRequestHandler(_commandProcessor!);
            }

            return new MyCommandHandlerAsync(_receivedMessages);
        });
        var messageMapperRegistry = new MessageMapperRegistry(null, new SimpleMessageMapperFactoryAsync(_ => new MyCommandMessageMapperAsync()));
        messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();
        var producer = new InMemoryMessageProducer(_internalBus)
        {
            Publication =
            {
                Topic = routingKey,
                RequestType = typeof(MyCommand)
            }
        };
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer }, });
        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
        var tracer = new BrighterTracer(_timeProvider);
        _outbox = new InMemoryOutbox(_timeProvider)
        {
            Tracer = tracer
        };
        IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry, resiliencePipelineRegistry, messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), _outbox);
        _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new DefaultPolicy(), resiliencePipelineRegistry, bus, new InMemorySchedulerFactory { TimeProvider = _timeProvider });
    }

    [Test]
    public async Task When_Scheduling_Send_With_Delay_A_Message_To_The_Command_Processor_Async()
    {
        await _commandProcessor.SendAsync(TimeSpan.FromSeconds(10), _myCommand);
        await Assert.That(_receivedMessages.Keys).DoesNotContain(nameof(MyCommandHandlerAsync));
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        await Assert.That(_receivedMessages.Keys).Contains(nameof(MyCommandHandlerAsync));
    }

    [Test]
    public async Task When_Scheduling_Send_With_At_A_Message_To_The_Command_Processor_Async()
    {
        await _commandProcessor.SendAsync(_timeProvider.GetUtcNow().AddSeconds(10), _myCommand);
        await Assert.That(_receivedMessages.Keys).DoesNotContain(nameof(MyCommandHandlerAsync));
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        await Assert.That(_receivedMessages.Keys).Contains(nameof(MyCommandHandlerAsync));
    }

    [Test]
    public async Task When_Scheduling_Publish_With_Delay_A_Message_To_The_Command_Processor_Async()
    {
        await _commandProcessor.PublishAsync(TimeSpan.FromSeconds(10), _myCommand);
        await Assert.That(_receivedMessages.Keys).DoesNotContain(nameof(MyCommandHandlerAsync));
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        await Assert.That(_receivedMessages.Keys).Contains(nameof(MyCommandHandlerAsync));
    }

    [Test]
    public async Task When_Scheduling_Publish_With_At_A_Message_To_The_Command_Processor_Async()
    {
        await _commandProcessor.PublishAsync(_timeProvider.GetUtcNow().AddSeconds(10), _myCommand);
        await Assert.That(_receivedMessages.Keys).DoesNotContain(nameof(MyCommandHandlerAsync));
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        await Assert.That(_receivedMessages.Keys).Contains(nameof(MyCommandHandlerAsync));
    }

    [Test]
    public async Task When_Scheduling_Post_With_At_A_Message_To_The_Command_Processor_Async()
    {
        await _commandProcessor.PostAsync(_timeProvider.GetUtcNow().AddSeconds(10), _myCommand);
        await Assert.That(_internalBus.Stream(new RoutingKey(Topic)).Any()).IsFalse();
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        await Assert.That(_internalBus.Stream(new RoutingKey(Topic)).Any()).IsTrue();
        var actual = await _outbox.GetAsync(_myCommand.Id, new RequestContext());
        await Assert.That(actual).IsNotNull();
        var expected = new Message(new MessageHeader(_myCommand.Id, new RoutingKey(Topic), MessageType.MT_COMMAND), new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options)));
        await Assert.That(actual.Body).IsEquivalentTo(expected.Body);
        await Assert.That(actual.Id).IsEqualTo(expected.Id);
        await Assert.That(actual.Persist).IsEqualTo(expected.Persist);
        await Assert.That(actual.Redelivered).IsEqualTo(expected.Redelivered);
        await Assert.That(actual.DeliveryTag).IsEqualTo(expected.DeliveryTag);
        await Assert.That(actual.Header.MessageType).IsEqualTo(expected.Header.MessageType);
        await Assert.That(actual.Header.Topic).IsEqualTo(expected.Header.Topic);
        await Assert.That((actual.Header.TimeStamp - expected.Header.TimeStamp).Duration()).IsLessThanOrEqualTo(TimeSpan.FromSeconds(1));
        await Assert.That(actual.Header.CorrelationId).IsEqualTo(expected.Header.CorrelationId);
        await Assert.That(actual.Header.ReplyTo).IsEqualTo(expected.Header.ReplyTo);
        await Assert.That(actual.Header.ContentType).IsEqualTo(expected.Header.ContentType);
        await Assert.That(actual.Header.HandledCount).IsEqualTo(expected.Header.HandledCount);
    }

    [Test]
    public async Task When_Scheduling_Post_With_Delay_A_Message_To_The_Command_Processor_Async()
    {
        await _commandProcessor.PostAsync(TimeSpan.FromSeconds(10), _myCommand);
        await Assert.That(_internalBus.Stream(new RoutingKey(Topic)).Any()).IsFalse();
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        await Assert.That(_internalBus.Stream(new RoutingKey(Topic)).Any()).IsTrue();
        var actual = await _outbox.GetAsync(_myCommand.Id, new RequestContext());
        await Assert.That(actual).IsNotNull();
        var expected = new Message(new MessageHeader(_myCommand.Id, new RoutingKey(Topic), MessageType.MT_COMMAND), new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options)));
        await Assert.That(actual.Body).IsEquivalentTo(expected.Body);
        await Assert.That(actual.Id).IsEqualTo(expected.Id);
        await Assert.That(actual.Persist).IsEqualTo(expected.Persist);
        await Assert.That(actual.Redelivered).IsEqualTo(expected.Redelivered);
        await Assert.That(actual.DeliveryTag).IsEqualTo(expected.DeliveryTag);
        await Assert.That(actual.Header.MessageType).IsEqualTo(expected.Header.MessageType);
        await Assert.That(actual.Header.Topic).IsEqualTo(expected.Header.Topic);
        await Assert.That((actual.Header.TimeStamp - expected.Header.TimeStamp).Duration()).IsLessThanOrEqualTo(TimeSpan.FromSeconds(1));
        await Assert.That(actual.Header.CorrelationId).IsEqualTo(expected.Header.CorrelationId);
        await Assert.That(actual.Header.ReplyTo).IsEqualTo(expected.Header.ReplyTo);
        await Assert.That(actual.Header.ContentType).IsEqualTo(expected.Header.ContentType);
        await Assert.That(actual.Header.HandledCount).IsEqualTo(expected.Header.HandledCount);
    }
}