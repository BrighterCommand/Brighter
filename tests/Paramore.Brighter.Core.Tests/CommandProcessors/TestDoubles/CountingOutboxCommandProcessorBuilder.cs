using System;
using System.Collections.Generic;
using System.Transactions;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

/// <summary>
/// Builds a <see cref="CommandProcessor"/> that posts <see cref="MyCommand"/> through an <see cref="OutboxProducerMediator{TMessage,TTransaction}"/>
/// on top of a <see cref="CountingOutbox"/>, with the outstanding limit and check interval chosen by the test.
/// It can post both with <c>Post</c> and with <c>PostAsync</c>.
/// </summary>
internal static class CountingOutboxCommandProcessorBuilder
{
    public static CommandProcessor Build(
        CountingOutbox outbox,
        int maxOutStandingMessages,
        TimeSpan maxOutStandingCheckInterval,
        TimeProvider timeProvider = null)
    {
        var routingKey = new RoutingKey("MyCommand");

        var producer = new InMemoryMessageProducer(
            new InternalBus(),
            new Publication { Topic = routingKey, RequestType = typeof(MyCommand) });

        var messageMapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new MyCommandMessageMapper()),
            new SimpleMessageMapperFactoryAsync(_ => new MyCommandMessageMapperAsync()));
        messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
        messageMapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();

        var producerRegistry = new ProducerRegistry(
            new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer } });

        var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();
        var tracer = new BrighterTracer();

        var mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
            producerRegistry: producerRegistry,
            resiliencePipelineRegistry: resiliencePipelineRegistry,
            mapperRegistry: messageMapperRegistry,
            messageTransformerFactory: new EmptyMessageTransformerFactory(),
            messageTransformerFactoryAsync: new EmptyMessageTransformerFactoryAsync(),
            tracer: tracer,
            publicationFinder: new FindPublicationByPublicationTopicOrRequestType(),
            outbox: outbox,
            maxOutStandingMessages: maxOutStandingMessages,
            maxOutStandingCheckInterval: maxOutStandingCheckInterval,
            timeProvider: timeProvider);

        return new CommandProcessor(
            new InMemoryRequestContextFactory(),
            new DefaultPolicy(),
            resiliencePipelineRegistry,
            mediator,
            new InMemorySchedulerFactory());
    }
}
