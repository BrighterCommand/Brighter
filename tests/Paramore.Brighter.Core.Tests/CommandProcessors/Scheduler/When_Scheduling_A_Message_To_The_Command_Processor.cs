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
using System.Threading.Tasks;
using System.Transactions;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Scheduler;

[Collection("CommandProcessor")]
public class CommandProcessorSchedulerCommandTests : IDisposable
{
    private const string Topic = "MyCommand";
    private readonly CommandProcessor _commandProcessor;
    private readonly MyCommand _myCommand;
    private readonly Message _message;
    private readonly InMemoryOutbox _outbox;
    private readonly InternalBus _internalBus = new();
    private readonly FakeTimeProvider _timeProvider;

    public CommandProcessorSchedulerCommandTests()
    {
        _myCommand = new MyCommand { Value = $"Hello World {Guid.NewGuid():N}" };

        var routingKey = new RoutingKey(Topic);

        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(DateTimeOffset.UtcNow);

        InMemoryProducer producer =
            new(_internalBus, _timeProvider) { Publication = { Topic = routingKey, RequestType = typeof(MyCommand) } };

        _message = new Message(
            new MessageHeader(_myCommand.Id, routingKey, MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
        );

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
            null);
        messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

        var retryPolicy = Policy
            .Handle<Exception>()
            .Retry();

        var circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

        var policyRegistry = new PolicyRegistry
        {
            { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy }
        };
        var producerRegistry =
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer }, });

        var tracer = new BrighterTracer(_timeProvider);
        _outbox = new InMemoryOutbox(_timeProvider) { Tracer = tracer };

        IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            _outbox
        );

        CommandProcessor.ClearServiceBus();
        _commandProcessor = new CommandProcessor(
            new InMemoryRequestContextFactory(),
            policyRegistry,
            bus,
            new InMemorySchedulerFactory { TimeProvider = _timeProvider }
        );
    }

    [Fact]
    public void When_Scheduling_With_Delay_A_Message_To_The_Command_Processor()
    {
        _commandProcessor.Post(TimeSpan.FromSeconds(10), _myCommand);
        _internalBus.Stream(new RoutingKey(Topic)).Any().Should().BeFalse();

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        _internalBus.Stream(new RoutingKey(Topic)).Any().Should().BeTrue();

        var message = _outbox.Get(_myCommand.Id, new RequestContext());
        message.Should().NotBeNull();
        message.Should().Be(_message);
    }

    [Fact]
    public void When_Scheduling_With_At_A_Message_To_The_Command_Processor()
    {
        _commandProcessor.Post(_timeProvider.GetUtcNow().AddSeconds(10), _myCommand);
        _internalBus.Stream(new RoutingKey(Topic)).Any().Should().BeFalse();

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        _internalBus.Stream(new RoutingKey(Topic)).Any().Should().BeTrue();

        var message = _outbox.Get(_myCommand.Id, new RequestContext());
        message.Should().NotBeNull();

        message.Should().Be(_message);
    }

    public void Dispose()
    {
        CommandProcessor.ClearServiceBus();
    }
}
