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
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Scheduler;

[Collection("CommandProcessor")]
public class SchedulerCommandTests : IDisposable
{
    private readonly RoutingKey _routingKey = new("MyCommand");
    private readonly CommandProcessor _commandProcessor;
    private readonly MyCommand _myCommand;
    private readonly Message _message;
    private readonly InMemoryOutbox _outbox;
    private readonly InternalBus _internalBus = new();
    private readonly FakeTimeProvider _timeProvider;

    public SchedulerCommandTests()
    {
        _myCommand = new() { Value = $"Hello World {Guid.NewGuid():N}" };

        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(DateTimeOffset.UtcNow);

        var tracer = new BrighterTracer(_timeProvider);
        _outbox = new InMemoryOutbox(_timeProvider) { Tracer = tracer };
        InMemoryProducer producer = new(_internalBus, _timeProvider)
        {
            Publication = { Topic = _routingKey, RequestType = typeof(MyCommand) }
        };

        _message = new Message(
            new MessageHeader(_myCommand.Id, _routingKey, MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
        );

        var messageMapperRegistry =
            new MessageMapperRegistry(
                new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
                null);
        messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

        var producerRegistry =
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { _routingKey, producer }, });

        var externalBus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry: producerRegistry,
            policyRegistry: new DefaultPolicy(),
            mapperRegistry: messageMapperRegistry,
            messageTransformerFactory: new EmptyMessageTransformerFactory(),
            messageTransformerFactoryAsync: new EmptyMessageTransformerFactoryAsync(),
            tracer: tracer,
            outbox: _outbox
        );

        _commandProcessor = CommandProcessorBuilder.StartNew()
            .Handlers(new HandlerConfiguration(new SubscriberRegistry(), new EmptyHandlerFactorySync()))
            .DefaultPolicy()
            .ExternalBus(ExternalBusType.FireAndForget, externalBus)
            .ConfigureInstrumentation(new BrighterTracer(TimeProvider.System), InstrumentationOptions.All)
            .RequestContextFactory(new InMemoryRequestContextFactory())
            .RequestSchedulerFactory(new InMemorySchedulerFactory { TimeProvider = _timeProvider })
            .Build();
    }

    [Fact]
    public void When_Scheduling_With_A_Default_Policy_And_Passing_A_Delay()
    {
        _commandProcessor.Post(TimeSpan.FromSeconds(10), _myCommand);
        _internalBus.Stream(new RoutingKey(_routingKey)).Any().Should().BeFalse();

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        _internalBus.Stream(new RoutingKey(_routingKey)).Any().Should().BeTrue();

        var message = _outbox.Get(_myCommand.Id, new RequestContext());
        message.Should().NotBeNull();
        message.Should().Be(_message);
    }

    [Fact]
    public void When_Scheduling_With_A_Default_Policy_And_Passing_An_At()
    {
        _commandProcessor.Post(_timeProvider.GetUtcNow().AddSeconds(10), _myCommand);
        _internalBus.Stream(new RoutingKey(_routingKey)).Any().Should().BeFalse();

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        _internalBus.Stream(new RoutingKey(_routingKey)).Any().Should().BeTrue();

        var message = _outbox.Get(_myCommand.Id, new RequestContext());
        message.Should().NotBeNull();
        message.Should().Be(_message);
    }

    public void Dispose()
    {
        CommandProcessor.ClearServiceBus();
    }

    internal class EmptyHandlerFactorySync : IAmAHandlerFactorySync
    {
        public IHandleRequests Create(Type handlerType, IAmALifetime lifetime)
        {
            return null;
        }

        public void Release(IHandleRequests handler, IAmALifetime lifetime) { }
    }
}
