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
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
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

        var retryPolicy = Policy
            .Handle<Exception>()
            .RetryAsync();

        var circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(1, TimeSpan.FromMilliseconds(1));

        var producer = new InMemoryProducer (_internalBus, _timeProvider) { Publication = { Topic = routingKey, RequestType = typeof(MyCommand) } };
        var policyRegistry = new PolicyRegistry
        {
            { CommandProcessor.RETRYPOLICYASYNC, retryPolicy }, { CommandProcessor.CIRCUITBREAKERASYNC, circuitBreakerPolicy }
        };
        
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer }, });

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
        _commandProcessor = new CommandProcessor(registry,
            handlerFactory,
            new InMemoryRequestContextFactory(),
            policyRegistry,
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

        var message = _outbox.Get(_myCommand.Id, new RequestContext());
        Assert.NotNull(message);
        Assert.Equivalent(new Message(
            new MessageHeader(_myCommand.Id, new RoutingKey(Topic), MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
        ), message);
    }

    [Fact]
    public async Task When_Scheduling_Post_With_Delay_A_Message_To_The_Command_Processor_Async()
    {
        await _commandProcessor.PostAsync(TimeSpan.FromSeconds(10), _myCommand);
        Assert.False(_internalBus.Stream(new RoutingKey(Topic)).Any());

        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        Assert.True(_internalBus.Stream(new RoutingKey(Topic)).Any());

        var message = _outbox.Get(_myCommand.Id, new RequestContext());
        Assert.NotNull(message);
        Assert.Equivalent(new Message(
            new MessageHeader(_myCommand.Id, new RoutingKey(Topic), MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize(_myCommand, JsonSerialisationOptions.Options))
        ), message);
    }
    
    public void Dispose()
    {
        CommandProcessor.ClearServiceBus();
    }
}
