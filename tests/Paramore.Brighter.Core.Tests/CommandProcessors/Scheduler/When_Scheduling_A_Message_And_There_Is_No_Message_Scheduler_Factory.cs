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
using System.Transactions;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.TestHelpers;
using Paramore.Brighter.Observability;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Scheduler;

[Collection("CommandProcessor")]
public class CommandSchedulerNoMessageSchedulerFactoryTests : IDisposable
{
    private readonly CommandProcessor _commandProcessor;
    private readonly MyCommand _myCommand = new MyCommand();
    private Exception _exception;

    public CommandSchedulerNoMessageSchedulerFactoryTests()
    {
        var routingKey = new RoutingKey("MyCommand");
        _myCommand.Value = "Hello World";

        var timeProvider = new FakeTimeProvider();
        InMemoryProducer producer = new(new InternalBus(), timeProvider)
        {
            Publication = {Topic = routingKey, RequestType = typeof(MyCommand)}
        };

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory((_) => new MyCommandMessageMapper()),
            null);

        var retryPolicy = Policy
            .Handle<Exception>()
            .Retry();

        var circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
        {
            { routingKey, producer },
        });

        var policyRegistry = new PolicyRegistry
        {
            { CommandProcessor.RETRYPOLICY, retryPolicy },
            { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy }
        };

        var tracer = new BrighterTracer(timeProvider);
        var outbox = new InMemoryOutbox(timeProvider) {Tracer = tracer};

        IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry, 
            policyRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            outbox
        );
        
        CommandProcessor.ClearServiceBus();
        _commandProcessor = new CommandProcessor(
            new InMemoryRequestContextFactory(), 
            policyRegistry,
            bus
        ); 
    }

    [Fact]
    public void When_Scheduling_A_Message_And_There_Is_No_Message_Scheduler_Factory()
    {
        _exception = Catch.Exception(() => _commandProcessor.SchedulerPost(_myCommand, TimeSpan.FromSeconds(1)));
        _exception.Should().BeOfType<InvalidOperationException>();
            
        _exception = Catch.Exception(() => _commandProcessor.SchedulerPost(_myCommand, DateTimeOffset.UtcNow.AddSeconds(10)));
        _exception.Should().BeOfType<InvalidOperationException>();
    }

    public void Dispose()
    {
        CommandProcessor.ClearServiceBus();
    }
}