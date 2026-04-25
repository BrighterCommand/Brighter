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
    // Pins the bulk-outbox-clear path: reply messages drained via BulkDispatchAsync
    // are grouped by (WireTopic, LookupTopic) and dispatched to the producer resolved
    // from LookupTopic. Prevents regression to a Header.Topic-only lookup that would
    // fail to locate the producer for reply messages during outbox sweeps.
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

            //act - drain via the bulk path (exercised by background outbox sweeps).
            //ClearOutstandingFromOutboxAsync awaits BackgroundDispatchUsingAsync which
            //awaits BulkDispatchAsync, so dispatch is complete when this returns.
            await _mediator.ClearOutstandingFromOutboxAsync(
                amountToClear: 10,
                minimumAge: TimeSpan.Zero,
                useBulk: true,
                requestContext: context);

            //assert - messages landed on the reply topic. If producer lookup had used
            //Header.Topic (reply address) instead of the bag's ProducerTopic, LookupBy
            //would have thrown and nothing would arrive on the bus.
            var messages = _internalBus.Stream(_replyTopic).ToArray();
            Assert.True(messages.Length == 2,
                $"expected 2 reply messages on the bus after bulk dispatch, got {messages.Length} — bulk dispatch or producer lookup failed");

            //assert - the ProducerTopic bag entry survives bulk dispatch so an InMemoryOutbox
            //(which holds the message by reference) keeps the producer hint for retries.
            //Wire-format stripping is the transport's responsibility (see MessageHeader.IsLocalHeader).
            Assert.All(messages, m =>
                Assert.True(m.Header.Bag.ContainsKey(Message.ProducerTopicHeaderName)));
        }
    }
}
