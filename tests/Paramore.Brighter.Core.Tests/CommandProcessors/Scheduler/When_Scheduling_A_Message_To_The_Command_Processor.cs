#region Licence

/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Scheduler;

[Collection("CommandProcessor")]
public class CommandProcessorSchedulerCommandTests : IDisposable
{
    private const string Topic = "MyCommand";
    private readonly CommandProcessor _commandProcessor;
    private readonly MyCommand _myCommand;
    private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
    private readonly FakeTimeProvider _timeProvider;
    private readonly InMemoryOutbox _outbox;
    private readonly InternalBus _internalBus = new();

    public CommandProcessorSchedulerCommandTests()
    {
        _myCommand = new() { Value = $"Hello World {Guid.NewGuid():N}" };
        var routingKey = new RoutingKey("MyCommand");
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(DateTimeOffset.UtcNow);

        var registry = new SubscriberRegistry();
        registry.RegisterAsync<FireSchedulerRequest, FireSchedulerRequestHandler>();
        registry.Register<MyCommand, MyCommandHandler>();
        var handlerFactory = new SimpleHandlerFactory(
            _ => new MyCommandHandler(_receivedMessages),
            _ => new FireSchedulerRequestHandler(_commandProcessor!));

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyCommandMessageMapper()),
            null);

        messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

        var producer = new InMemoryMessageProducer(_internalBus, _timeProvider, new Publication { Topic = routingKey, RequestType = typeof(MyCommand) });

        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>()
            .AddBrighterDefault();

        var producerRegistry =
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer }, });

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
    public void When_Scheduling_Send_With_Delay_A_Message_To_The_Command_Processor()
    {
        _commandProcessor.Send(TimeSpan.FromSeconds(10), _myCommand);

        Assert.DoesNotContain(nameof(MyCommandHandler), _receivedMessages);

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        Assert.Contains(nameof(MyCommandHandler), _receivedMessages);
    }

    [Fact]
    public void When_Scheduling_Send_With_At_A_Message_To_The_Command_Processor()
    {
        _commandProcessor.Send(_timeProvider.GetUtcNow().AddSeconds(10), _myCommand);

        Assert.DoesNotContain(nameof(MyCommandHandler), _receivedMessages);

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        Assert.Contains(nameof(MyCommandHandler), _receivedMessages);
    }

    [Fact]
    public void When_Scheduling_Publish_With_Delay_A_Message_To_The_Command_Processor()
    {
        _commandProcessor.Publish(TimeSpan.FromSeconds(10), _myCommand);

        Assert.DoesNotContain(nameof(MyCommandHandler), _receivedMessages);

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        Assert.Contains(nameof(MyCommandHandler), _receivedMessages);
    }

    [Fact]
    public void When_Scheduling_Publish_With_At_A_Message_To_The_Command_Processor()
    {
        _commandProcessor.Publish(_timeProvider.GetUtcNow().AddSeconds(10), _myCommand);

        Assert.DoesNotContain(nameof(MyCommandHandler), _receivedMessages);

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        Assert.Contains(nameof(MyCommandHandler), _receivedMessages);
    }

    [Fact]
    public void When_Scheduling_Post_With_At_A_Message_To_The_Command_Processor()
    {
        _commandProcessor.Post(_timeProvider.GetUtcNow().AddSeconds(10), _myCommand);
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
    public void When_Scheduling_Post_With_Delay_A_Message_To_The_Command_Processor()
    {
        _commandProcessor.Post(TimeSpan.FromSeconds(10), _myCommand);
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
