using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post;

/// <summary>
/// Regression for PR #4254 review finding 1, send side. The mediator releases the wrap pipeline back to its
/// factories after producing the message. A throwing mapper/transform release must not abort a send whose
/// message has already been built: the request mapped, so a cleanup-path failure is logged, not surfaced to
/// the caller of <c>Post</c>. Proved RED against the pre-fix <c>using var pipeline</c> shape, where the
/// disposal exception escaped <c>MapMessage</c> and the message was never posted.
/// </summary>
public class CommandProcessorPostMapperReleaseThrowsTests
{
    private const string Topic = "MyCommand";
    private readonly CommandProcessor _commandProcessor;
    private readonly MyCommand _myCommand = new() { Value = "Hello World" };
    private readonly InternalBus _internalBus = new();
    private readonly RoutingKey _routingKey = new(Topic);

    public CommandProcessorPostMapperReleaseThrowsTests()
    {
        var timeProvider = new FakeTimeProvider();

        InMemoryMessageProducer messageProducer = new(_internalBus,
            new Publication { Topic = _routingKey, RequestType = typeof(MyCommand) });

        var messageMapperRegistry = new MessageMapperRegistry(new ThrowingOnReleaseMessageMapperFactory(), null);
        messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();

        var producerRegistry = new ProducerRegistry(
            new Dictionary<RoutingKey, IAmAMessageProducer> { { _routingKey, messageProducer } });

        var tracer = new BrighterTracer(timeProvider);

        IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry,
            resiliencePipelineRegistry,
            messageMapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer,
            new FindPublicationByPublicationTopicOrRequestType(),
            new InMemoryOutbox(timeProvider) { Tracer = tracer }
        );

        _commandProcessor = new CommandProcessor(
            new InMemoryRequestContextFactory(),
            new DefaultPolicy(),
            resiliencePipelineRegistry,
            bus,
            new InMemorySchedulerFactory()
        );
    }

    [Fact]
    public void When_a_mapper_release_throws_the_message_is_still_posted()
    {
        _commandProcessor.Post(_myCommand);

        //the message mapped, so the send must complete and reach the transport — a throwing release must not abort it
        Assert.Single(_internalBus.Stream(_routingKey));
    }

    private sealed class ThrowingOnReleaseMessageMapperFactory : IAmAMessageMapperFactory
    {
        public Lease<IAmAMessageMapper>? Create(Type messageMapperType) => new Lease<IAmAMessageMapper>(new MyCommandMessageMapper());

        public void Release(Lease<IAmAMessageMapper> lease) =>
            throw new InvalidOperationException("mapper release failed");
    }
}
