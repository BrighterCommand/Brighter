using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.UnresolvableMapper;

/// <summary>
/// Characterizes PR #4254 review finding 1(b), reply side. <c>CreateRequestFromMessage</c> prefers the async
/// unwrap pipeline and falls through to the sync one. Because <c>HasPipeline</c> now resolves the mapper
/// <em>type</em> rather than creating a probe instance, an async mapper type that is registered but cannot
/// be instantiated makes the async <c>HasPipeline</c> <c>true</c>, so the async build is attempted and throws
/// a <c>ConfigurationException</c> — it no longer silently falls through to the working sync pipeline. RED on
/// master, where the async probe returned null, the async check was <c>false</c>, and the reply was unwrapped
/// by the sync mapper. The normal fall-through — no async mapper registered at all — is unchanged.
/// </summary>
public class MediatorUnresolvableMapperReplyTests
{
    private const string Topic = "MyCommand";
    private readonly RoutingKey _routingKey = new(Topic);
    private readonly OutboxProducerMediator<Message, CommittableTransaction> _mediator;
    private readonly Message _message;

    public MediatorUnresolvableMapperReplyTests()
    {
        var timeProvider = new FakeTimeProvider();

        InMemoryMessageProducer messageProducer = new(new InternalBus(),
            new Publication { Topic = _routingKey, RequestType = typeof(MyCommand) });

        //sync mapper works; the async mapper type is registered but its factory cannot instantiate it
        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyCommandMessageMapper()),
            new NullReturningMapperFactoryAsync());
        messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
        messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();

        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
        var producerRegistry = new ProducerRegistry(
            new Dictionary<RoutingKey, IAmAMessageProducer> { { _routingKey, messageProducer } });
        var tracer = new BrighterTracer(timeProvider);

        _mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            resiliencePipelineRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            new FindPublicationByPublicationTopicOrRequestType(),
            new InMemoryOutbox(timeProvider) { Tracer = tracer }
        );

        //a genuinely round-trippable reply, so that on master the sync fall-through would have succeeded
        var context = new InMemoryRequestContextFactory().Create();
        _message = new MyCommandMessageMapper { Context = context }
            .MapToMessage(new MyCommand { Value = "Hello World" },
                new Publication { Topic = _routingKey, RequestType = typeof(MyCommand) });
    }

    [Fact]
    public void When_the_async_mapper_is_unresolvable_the_reply_does_not_fall_through_to_sync()
    {
        var context = new InMemoryRequestContextFactory().Create();

        Assert.Throws<ConfigurationException>(
            () => _mediator.CreateRequestFromMessage<MyCommand>(_message, context, out _));
    }

    private sealed class NullReturningMapperFactoryAsync : IAmAMessageMapperFactoryAsync
    {
        public IAmAMessageMapperAsync Create(Type messageMapperType) => null!;

        public void Release(IAmAMessageMapperAsync mapper) { }

        public ValueTask ReleaseAsync(IAmAMessageMapperAsync mapper) => default;
    }
}
