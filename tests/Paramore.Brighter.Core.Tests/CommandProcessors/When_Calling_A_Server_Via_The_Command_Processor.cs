using System;
using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.ServiceActivator.TestHelpers;
using Polly;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors
{
    [Collection("CommandProcessor")]
    public class CommandProcessorCallTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyRequest _myRequest = new MyRequest();
        private readonly Message _message;
        private readonly FakeMessageProducerWithPublishConfirmation _fakeMessageProducerWithPublishConfirmation;


        public CommandProcessorCallTests()
        {
            _myRequest.RequestValue = "Hello World";

            _fakeMessageProducerWithPublishConfirmation = new FakeMessageProducerWithPublishConfirmation();

            const string topic = "MyRequest";
            var header = new MessageHeader(
                messageId: _myRequest.Id,
                topic: topic,
                messageType:MessageType.MT_COMMAND,
                correlationId: _myRequest.ReplyAddress.CorrelationId,
                replyTo: _myRequest.ReplyAddress.Topic);

            var body = new MessageBody(JsonSerializer.Serialize(new MyRequestDTO(_myRequest.Id.ToString(), _myRequest.RequestValue), JsonSerialisationOptions.Options));
            _message = new Message(header, body);

            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory((type) =>
            {
                if (type == typeof(MyRequestMessageMapper))
                    return new MyRequestMessageMapper();
                if (type == typeof(MyResponseMessageMapper))
                    return new MyResponseMessageMapper();

                throw new ConfigurationException($"No mapper found for {type.Name}");
            }));
            messageMapperRegistry.Register<MyRequest, MyRequestMessageMapper>();
            messageMapperRegistry.Register<MyResponse, MyResponseMessageMapper>();

            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyResponse, MyResponseHandler>();
            var handlerFactory = new TestHandlerFactorySync<MyResponse, MyResponseHandler>(() => new MyResponseHandler());

            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            InMemoryChannelFactory inMemoryChannelFactory = new InMemoryChannelFactory();
            //we need to seed the response as the fake producer does not actually send across the wire
            inMemoryChannelFactory.SeedChannel(new[] {_message});

            var replySubs = new List<Subscription>
            {
                new Subscription<MyResponse>()
            };

            _commandProcessor = new CommandProcessor(
                subscriberRegistry,
                handlerFactory,
                new InMemoryRequestContextFactory(),
                new PolicyRegistry { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } },
                messageMapperRegistry,
                new InMemoryOutbox(),
                new ProducerRegistry(new Dictionary<string, IAmAMessageProducer> {{topic, _fakeMessageProducerWithPublishConfirmation},}),
                replySubs,
                responseChannelFactory: inMemoryChannelFactory);

            PipelineBuilder<MyRequest>.ClearPipelineCache();
        }

        [Fact]
        public void When_Calling_A_Server_Via_The_Command_Processor()
        {
            _commandProcessor.Call<MyRequest, MyResponse>(_myRequest, 500);

            //should send a message via the messaging gateway
            _fakeMessageProducerWithPublishConfirmation.MessageWasSent.Should().BeTrue();

            //should convert the command into a message
            _fakeMessageProducerWithPublishConfirmation.SentMessages[0].Should().Be(_message);

            //should forward response to a handler
            MyResponseHandler.ShouldReceive(new MyResponse(_myRequest.ReplyAddress) {Id = _myRequest.Id});
        }

        public void Dispose()
        {
            CommandProcessor.ClearExtServiceBus();
        }
    }
}
