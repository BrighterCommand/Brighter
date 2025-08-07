using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Scheduler;

[Collection("CommandProcessor")]
public class CommandProcessorSchedulerCommandAsyncTests : IDisposable
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
        _myCommand = new() { Value = $"Hello World {Guid.NewGuid():N}" };
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

        var messageMapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyCommandMessageMapperAsync()));
        
        messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();

        var producer = new InMemoryMessageProducer (_internalBus, _timeProvider) { Publication = { Topic = routingKey, RequestType = typeof(MyCommand) } };
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer }, });
        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>()
            .AddBrighterDefault();

        var tracer = new BrighterTracer(_timeProvider);
        _outbox = new InMemoryOutbox(_timeProvider) { Tracer = tracer };

        IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            resiliencePipelineRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            new FindPublicationByPublicationTopicOrRequestType(),
            _outbox
        );

        CommandProcessor.ClearServiceBus();
        _commandProcessor = new CommandProcessor(registry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            new DefaultPolicy(),
            resiliencePipelineRegistry,
            bus,
            new InMemorySchedulerFactory { TimeProvider = _timeProvider });
        PipelineBuilder<MyCommand>.ClearPipelineCache();
        PipelineBuilder<FireSchedulerRequest>.ClearPipelineCache();
    }

    [Fact]
    public async Task When_Scheduling_Send_With_Delay_A_Message_To_The_Command_Processor_Async()
    {
        await _commandProcessor.SendAsync(TimeSpan.FromSeconds(10), _myCommand);

        Assert.DoesNotContain(nameof(MyCommandHandlerAsync), _receivedMessages);

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        Assert.Contains(nameof(MyCommandHandlerAsync), _receivedMessages);
    }

    [Fact]
    public async Task When_Scheduling_Send_With_At_A_Message_To_The_Command_Processor_Async()
    {
        await _commandProcessor.SendAsync(_timeProvider.GetUtcNow().AddSeconds(10), _myCommand);

        Assert.DoesNotContain(nameof(MyCommandHandlerAsync), _receivedMessages);

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        Assert.Contains(nameof(MyCommandHandlerAsync), _receivedMessages);
    }

    [Fact]
    public async Task When_Scheduling_Publish_With_Delay_A_Message_To_The_Command_Processor_Async()
    {
        await _commandProcessor.PublishAsync(TimeSpan.FromSeconds(10), _myCommand);

        Assert.DoesNotContain(nameof(MyCommandHandlerAsync), _receivedMessages);

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        Assert.Contains(nameof(MyCommandHandlerAsync), _receivedMessages);
    }

    [Fact]
    public async Task When_Scheduling_Publish_With_At_A_Message_To_The_Command_Processor_Async()
    {
        await _commandProcessor.PublishAsync(_timeProvider.GetUtcNow().AddSeconds(10), _myCommand);

        Assert.DoesNotContain(nameof(MyCommandHandlerAsync), _receivedMessages);

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        Assert.Contains(nameof(MyCommandHandlerAsync), _receivedMessages);
    }

    [Fact]
    public async Task When_Scheduling_Post_With_At_A_Message_To_The_Command_Processor_Async()
    {
        await _commandProcessor.PostAsync(_timeProvider.GetUtcNow().AddSeconds(10), _myCommand);
        Assert.False(_internalBus.Stream(new RoutingKey(Topic)).Any());

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        Assert.True(_internalBus.Stream(new RoutingKey(Topic)).Any());

        var actual = _outbox.Get(_myCommand.Id, new RequestContext());
        Assert.NotNull(actual);
        
        var expected = new Message(
            new MessageHeader(_myCommand.Id, new RoutingKey(Topic), MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
        );
        
        Assert.Equivalent(expected.Body, actual.Body);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Persist, actual.Persist);
        Assert.Equal(expected.Redelivered, actual.Redelivered);
        Assert.Equal(expected.DeliveryTag, actual.DeliveryTag);
        Assert.Equal(expected.Header.MessageType, actual.Header.MessageType);
        Assert.Equal(expected.Header.Topic, actual.Header.Topic);
        Assert.Equal(expected.Header.TimeStamp, actual.Header.TimeStamp, TimeSpan.FromSeconds(1));
        Assert.Equal(expected.Header.CorrelationId, actual.Header.CorrelationId);
        Assert.Equal(expected.Header.ReplyTo, actual.Header.ReplyTo);
        Assert.Equal(expected.Header.ContentType, actual.Header.ContentType);
        Assert.Equal(expected.Header.HandledCount, actual.Header.HandledCount);
    }

    [Fact]
    public async Task When_Scheduling_Post_With_Delay_A_Message_To_The_Command_Processor_Async()
    {
        await _commandProcessor.PostAsync(TimeSpan.FromSeconds(10), _myCommand);
        Assert.False(_internalBus.Stream(new RoutingKey(Topic)).Any());

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        Assert.True(_internalBus.Stream(new RoutingKey(Topic)).Any());

        var actual = _outbox.Get(_myCommand.Id, new RequestContext());
        Assert.NotNull(actual);
        
        var expected = new Message(
            new MessageHeader(_myCommand.Id, new RoutingKey(Topic), MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
        );
        
        Assert.Equivalent(expected.Body, actual.Body);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Persist, actual.Persist);
        Assert.Equal(expected.Redelivered, actual.Redelivered);
        Assert.Equal(expected.DeliveryTag, actual.DeliveryTag);
        Assert.Equal(expected.Header.MessageType, actual.Header.MessageType);
        Assert.Equal(expected.Header.Topic, actual.Header.Topic);
        Assert.Equal(expected.Header.TimeStamp, actual.Header.TimeStamp, TimeSpan.FromSeconds(1));
        Assert.Equal(expected.Header.CorrelationId, actual.Header.CorrelationId);
        Assert.Equal(expected.Header.ReplyTo, actual.Header.ReplyTo);
        Assert.Equal(expected.Header.ContentType, actual.Header.ContentType);
        Assert.Equal(expected.Header.HandledCount, actual.Header.HandledCount);
        
    }
    
    public void Dispose()
    {
        CommandProcessor.ClearServiceBus();
    }
}
