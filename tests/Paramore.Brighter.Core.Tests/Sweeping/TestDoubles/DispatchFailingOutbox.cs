#region License

/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Paramore.Brighter.Extensions;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// An in-memory outbox whose orders producer refuses every message and whose registry has no producer for the unrouted topic,
/// so a sweep trips orders and then throws when it reaches a message addressed to the unrouted topic.
/// </summary>
public sealed class DispatchFailingOutbox : IDisposable
{
    private readonly ServiceProvider _provider;

    /// <summary>Creates the outbox and mediator, sharing <paramref name="outboxCircuitBreaker"/> with the mediator.</summary>
    /// <param name="outboxCircuitBreaker">The breaker the mediator trips topics on.</param>
    public DispatchFailingOutbox(IAmAnOutboxCircuitBreaker outboxCircuitBreaker)
    {
        Mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { Orders, new TopicFailingProducerAsync(Orders, fails: true) }
            }),
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), null),
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer: null,
            new FindPublicationByPublicationTopicOrRequestType(),
            Outbox,
            outboxCircuitBreaker: outboxCircuitBreaker);

        var services = new ServiceCollection();
        services.AddSingleton<IAmAnOutboxProducerMediator>(Mediator);
        _provider = services.BuildServiceProvider();
    }

    /// <summary>The orders topic, whose producer refuses every message.</summary>
    public RoutingKey Orders { get; } = new("orders");

    /// <summary>A topic with no producer registered, so dispatching to it throws.</summary>
    public RoutingKey Unrouted { get; } = new("unrouted");

    /// <summary>The outbox the sweep reads undispatched messages from.</summary>
    public InMemoryOutbox Outbox { get; } = new(TimeProvider.System);

    /// <summary>The mediator a test can sweep directly.</summary>
    public OutboxProducerMediator<Message, CommittableTransaction> Mediator { get; }

    /// <summary>The scope factory the sweeper resolves the mediator through.</summary>
    public IServiceScopeFactory ScopeFactory => _provider.GetRequiredService<IServiceScopeFactory>();

    /// <summary>Adds a message for <paramref name="topic"/> that has not been dispatched.</summary>
    /// <param name="topic">The topic to address the message to.</param>
    public void AddUndispatched(RoutingKey topic)
        => Outbox.Add(
            new Message(new MessageHeader(Id.Random(), topic, MessageType.MT_COMMAND), new MessageBody("awaiting dispatch")),
            new RequestContext());

    /// <inheritdoc />
    public void Dispose() => _provider.Dispose();
}
