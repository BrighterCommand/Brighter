#region Licence
/* The MIT License (MIT)
Copyright � 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the �Software�), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED �AS IS�, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
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
using Paramore.Brighter.Extensions;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Scheduler.Events;
using Paramore.Brighter.Scheduler.Handlers;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Scheduler;
public class CommandProcessorSchedulerCommandTests
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
        _myCommand = new()
        {
            Value = $"Hello World {Guid.NewGuid():N}"};
        var routingKey = new RoutingKey("MyCommand");
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(DateTimeOffset.UtcNow);
        var registry = new SubscriberRegistry();
        registry.RegisterAsync<FireSchedulerRequest, FireSchedulerRequestHandler>();
        registry.Register<MyCommand, MyCommandHandler>();
        var handlerFactory = new SimpleHandlerFactory(_ => new MyCommandHandler(_receivedMessages), _ => new FireSchedulerRequestHandler(_commandProcessor!));
        var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => new MyCommandMessageMapper()), null);
        messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
        var producer = new InMemoryMessageProducer(_internalBus, new Publication { Topic = routingKey, RequestType = typeof(MyCommand) });
        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer }, });
        var tracer = new BrighterTracer(_timeProvider);
        _outbox = new InMemoryOutbox(_timeProvider)
        {
            Tracer = tracer
        };
        IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(producerRegistry, resiliencePipelineRegistry, messageMapperRegistry, new EmptyMessageTransformerFactory(), new EmptyMessageTransformerFactoryAsync(), tracer, new FindPublicationByPublicationTopicOrRequestType(), _outbox);
        _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new DefaultPolicy(), resiliencePipelineRegistry, bus, new InMemorySchedulerFactory { TimeProvider = _timeProvider });
    }

    [Test]
    public async Task When_Scheduling_Send_With_Delay_A_Message_To_The_Command_Processor()
    {
        _commandProcessor.Send(TimeSpan.FromSeconds(10), _myCommand);
        await Assert.That(_receivedMessages.Keys).DoesNotContain(nameof(MyCommandHandler));
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        await Assert.That(_receivedMessages.Keys).Contains(nameof(MyCommandHandler));
    }

    [Test]
    public async Task When_Scheduling_Send_With_At_A_Message_To_The_Command_Processor()
    {
        _commandProcessor.Send(_timeProvider.GetUtcNow().AddSeconds(10), _myCommand);
        await Assert.That(_receivedMessages.Keys).DoesNotContain(nameof(MyCommandHandler));
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        await Assert.That(_receivedMessages.Keys).Contains(nameof(MyCommandHandler));
    }

    [Test]
    public async Task When_Scheduling_Publish_With_Delay_A_Message_To_The_Command_Processor()
    {
        _commandProcessor.Publish(TimeSpan.FromSeconds(10), _myCommand);
        await Assert.That(_receivedMessages.Keys).DoesNotContain(nameof(MyCommandHandler));
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        await Assert.That(_receivedMessages.Keys).Contains(nameof(MyCommandHandler));
    }

    [Test]
    public async Task When_Scheduling_Publish_With_At_A_Message_To_The_Command_Processor()
    {
        _commandProcessor.Publish(_timeProvider.GetUtcNow().AddSeconds(10), _myCommand);
        await Assert.That(_receivedMessages.Keys).DoesNotContain(nameof(MyCommandHandler));
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        await Assert.That(_receivedMessages.Keys).Contains(nameof(MyCommandHandler));
    }

    [Test]
    public async Task When_Scheduling_Post_With_At_A_Message_To_The_Command_Processor()
    {
        _commandProcessor.Post(_timeProvider.GetUtcNow().AddSeconds(10), _myCommand);
        await Assert.That(_internalBus.Stream(new RoutingKey(Topic)).Any()).IsFalse();
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        await Assert.That(_internalBus.Stream(new RoutingKey(Topic)).Any()).IsTrue();
        var actual = await _outbox.GetAsync(_myCommand.Id, new RequestContext());
        await Assert.That(actual).IsNotNull();
        var expected = new Message(new MessageHeader(_myCommand.Id, new RoutingKey(Topic), MessageType.MT_COMMAND), new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options)));
        await Assert.That(actual.Body).IsEqualTo(expected.Body);
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
    public async Task When_Scheduling_Post_With_Delay_A_Message_To_The_Command_Processor()
    {
        _commandProcessor.Post(TimeSpan.FromSeconds(10), _myCommand);
        await Assert.That(_internalBus.Stream(new RoutingKey(Topic)).Any()).IsFalse();
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        await Assert.That(_internalBus.Stream(new RoutingKey(Topic)).Any()).IsTrue();
        var actual = await _outbox.GetAsync(_myCommand.Id, new RequestContext());
        await Assert.That(actual).IsNotNull();
        var expected = new Message(new MessageHeader(_myCommand.Id, new RoutingKey(Topic), MessageType.MT_COMMAND), new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options)));
        await Assert.That(actual.Body).IsEqualTo(expected.Body);
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