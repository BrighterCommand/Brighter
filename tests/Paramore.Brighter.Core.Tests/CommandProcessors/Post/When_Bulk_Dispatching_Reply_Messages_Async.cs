using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post
{
    // Gap: BulkDispatchAsync groups by Header.Topic (the mapper-set reply topic) but
    // looks up the producer using the first message's bag. The single-message Dispatch
    // paths are covered; this pins the bulk-outbox-clear path so a future refactor of
    // GetProducerLookupTopic or the firstMessage assumption cannot silently regress and
    // fail to locate the producer when draining the outbox via the bulk sweep.
    public class CommandProcessorBulkDispatchReplyAsyncTests
    {
        private const string ProducerTopic = "Reply";
        private readonly CommandProcessor _commandProcessor;
        private readonly IAmAnOutboxProducerMediator _mediator;
        private readonly MyResponse _replyOne;
        private readonly MyResponse _replyTwo;
        private readonly InternalBus _internalBus = new();
        private readonly RoutingKey _replyTopic;

        public CommandProcessorBulkDispatchReplyAsyncTests()
        {
            var timeProvider = new FakeTimeProvider();
            var producerRoutingKey = new RoutingKey(ProducerTopic);

            _replyTopic = new RoutingKey(Uuid.NewAsString());
            var replyAddress = new ReplyAddress(_replyTopic, Uuid.NewAsString());
            _replyOne = new MyResponse(replyAddress) { ReplyValue = "Hello" };
            _replyTwo = new MyResponse(replyAddress) { ReplyValue = "World" };

            InMemoryMessageProducer messageProducer = new(_internalBus,
                new Publication
                {
                    Topic = producerRoutingKey,
                    RequestType = typeof(MyResponse)
                });

            var messageMapperRegistry = new MessageMapperRegistry(
                null,
                new SimpleMessageMapperFactoryAsync(_ => new MyResponseMessageMapperAsync()));
            messageMapperRegistry.RegisterAsync<MyResponse, MyResponseMessageMapperAsync>();

            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>()
                .AddBrighterDefault();

            var producerRegistry = new ProducerRegistry(
                new Dictionary<RoutingKey, IAmAMessageProducer>
                {
                    { producerRoutingKey, messageProducer }
                });

            var tracer = new BrighterTracer(timeProvider);
            var outbox = new InMemoryOutbox(timeProvider) { Tracer = tracer };

            _mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry,
                resiliencePipelineRegistry,
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                outbox,
                maxOutStandingMessages: -1
            );

            _commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                new DefaultPolicy(),
                resiliencePipelineRegistry,
                _mediator,
                new InMemorySchedulerFactory()
            );
        }

        [Fact]
        public async Task When_Bulk_Dispatching_Reply_Messages_Async()
        {
            //arrange - deposit two replies whose mapper sets Header.Topic to the reply
            //address, not the registered producer topic
            var context = new RequestContext();
            await _commandProcessor.DepositPostAsync<MyResponse>([_replyOne, _replyTwo], context);

            //act - drain via the bulk path (exercised by background outbox sweeps)
            await _mediator.ClearOutstandingFromOutboxAsync(
                amountToClear: 10,
                minimumAge: TimeSpan.Zero,
                useBulk: true,
                requestContext: context);

            //allow background clear to run
            await Task.Delay(500);

            //assert - messages landed on the reply topic. If producer lookup had used
            //Header.Topic (reply address) instead of the bag's ProducerTopic, LookupBy
            //would have thrown and nothing would arrive on the bus.
            var messages = _internalBus.Stream(_replyTopic).ToArray();
            Assert.Equal(2, messages.Length);
        }
    }
}
