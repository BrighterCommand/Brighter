using System;
using System.Collections.Generic;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.UnresolvableMapper;

/// <summary>
/// Characterizes PR #4254 review finding 1(a), send side. <c>HasPipeline</c> now answers by resolving the
/// mapper <em>type</em> rather than creating a probe instance, so when a mapper type is registered but its
/// instance cannot be built (here a factory whose <c>Create</c> returns null), the send path no longer
/// short-circuits to <c>ArgumentOutOfRangeException("No message mapper defined for request")</c>. Instead
/// <c>HasPipeline</c> is <c>true</c>, the wrap pipeline build fails, and a <c>ConfigurationException</c> is
/// thrown wrapping the underlying <c>InvalidOperationException</c>. RED on master, where the probe create
/// returned null and the mediator threw the argument exception.
/// </summary>
public class MediatorUnresolvableMapperSendTests
{
    private const string Topic = "MyCommand";
    private readonly RoutingKey _routingKey = new(Topic);
    private readonly OutboxProducerMediator<Message, CommittableTransaction> _mediator;

    public MediatorUnresolvableMapperSendTests()
    {
        var timeProvider = new FakeTimeProvider();

        InMemoryMessageProducer messageProducer = new(new InternalBus(),
            new Publication { Topic = _routingKey, RequestType = typeof(MyCommand) });

        //the type is registered, but the factory cannot instantiate it (Create returns null)
        var messageMapperRegistry = new MessageMapperRegistry(new NullReturningMapperFactory(), null);
        messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

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
    }

    [Fact]
    public void When_the_registered_mapper_type_is_unresolvable_the_send_throws_a_configuration_error()
    {
        var context = new InMemoryRequestContextFactory().Create();

        var exception = Assert.Throws<ConfigurationException>(
            () => _mediator.CreateMessageFromRequest(new MyCommand { Value = "Hello World" }, context));

        //the outer is ConfigurationException (our standard), and the inner carries the real failure —
        //the type was found in the registry, but the factory could not build the instance
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    private sealed class NullReturningMapperFactory : IAmAMessageMapperFactory
    {
        public Lease<IAmAMessageMapper>? Create(Type messageMapperType) => null;

        public void Release(Lease<IAmAMessageMapper> lease) { }
    }
}
