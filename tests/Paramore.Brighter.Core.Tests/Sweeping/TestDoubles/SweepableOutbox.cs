using System;
using System.Collections.Generic;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;

/// <summary>
/// An in-memory outbox wired to an <see cref="InternalBus"/> through a real
/// <see cref="OutboxProducerMediator{TMessage,TTransaction}"/>, as a sweeper sees it from DI.
/// </summary>
public sealed class SweepableOutbox : IDisposable
{
    private readonly ServiceProvider _provider;

    public SweepableOutbox()
    {
        var producer = new InMemoryMessageProducer(Bus, new Publication { Topic = RoutingKey });
        var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { RoutingKey, producer } }),
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            new MessageMapperRegistry(new SimpleMessageMapperFactory(_ => null!), null),
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            new BrighterTracer(TimeProvider.System),
            new FindPublicationByPublicationTopicOrRequestType(),
            Outbox);

        var services = new ServiceCollection();
        services.AddSingleton<IAmAnOutboxProducerMediator>(mediator);
        _provider = services.BuildServiceProvider();
    }

    public RoutingKey RoutingKey { get; } = new("MyCommand");
    public InternalBus Bus { get; } = new();
    public InMemoryOutbox Outbox { get; } = new(TimeProvider.System);
    public IServiceScopeFactory ScopeFactory => _provider.GetRequiredService<IServiceScopeFactory>();

    public Message AddUndispatched()
    {
        var message = new Message(
            new MessageHeader(Id.Random(), RoutingKey, MessageType.MT_COMMAND),
            new MessageBody("awaiting a publish confirmation that never came"));
        Outbox.Add(message, new RequestContext());
        return message;
    }

    public void Dispose() => _provider.Dispose();
}
