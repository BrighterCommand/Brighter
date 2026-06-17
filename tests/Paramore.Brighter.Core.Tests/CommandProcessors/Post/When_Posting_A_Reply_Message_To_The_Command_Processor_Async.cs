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
    public class CommandProcessorPostReplyAsyncTests
    {
        private const string ProducerTopic = "Reply";
        private readonly CommandProcessor _commandProcessor;
        private readonly MyResponse _myResponse;
        private readonly InMemoryOutbox _outbox;
        private readonly InternalBus _internalBus = new();

        public CommandProcessorPostReplyAsyncTests()
        {
            var timeProvider = new FakeTimeProvider();
            var producerRoutingKey = new RoutingKey(ProducerTopic);

            var replyTopic = new RoutingKey(Uuid.NewAsString());
            var replyAddress = new ReplyAddress(replyTopic, Uuid.NewAsString());
            _myResponse = new MyResponse(replyAddress) { ReplyValue = "Hello World" };

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
            _outbox = new InMemoryOutbox(timeProvider) { Tracer = tracer };

            IAmAnOutboxProducerMediator bus = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry,
                resiliencePipelineRegistry,
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                _outbox
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
        public async Task When_Posting_A_Reply_Message_To_The_Command_Processor_Async()
        {
            //act - post a Reply whose mapper sets a dynamic topic (reply address)
            //different from the producer's registered topic
            await _commandProcessor.PostAsync(_myResponse);

            //assert - message was dispatched to the reply topic, not the producer topic
            var messages = _internalBus.Stream(_myResponse.SendersAddress.Topic).ToArray();
            Assert.Single(messages);

            //assert - message was stored in the outbox
            var outboxMessage = await _outbox.GetAsync(_myResponse.Id, new RequestContext());
            Assert.NotNull(outboxMessage);

            //assert - message topic is the reply address
            Assert.Equal(_myResponse.SendersAddress.Topic, outboxMessage.Header.Topic);

            //assert - the ProducerTopic bag entry survives dispatch so an InMemoryOutbox
            //(which holds the message by reference) keeps the producer hint for retries.
            //Wire-format stripping is the transport's responsibility (see MessageHeader.IsLocalHeader).
            Assert.True(messages[0].Header.Bag.ContainsKey(Message.ProducerTopicHeaderName));
        }
    }
}
