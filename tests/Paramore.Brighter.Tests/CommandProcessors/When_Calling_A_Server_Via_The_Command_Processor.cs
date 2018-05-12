using System;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Polly;
using Xunit;

namespace Paramore.Brighter.Tests.CommandProcessors
{
    public class CommandProcessorCallTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyRequest _myRequest = new MyRequest();
        private readonly Message _message;
        private readonly FakeMessageProducer _fakeMessageProducer;


        public CommandProcessorCallTests()
        {
            _myRequest.RequestValue = "Hello World";

            _fakeMessageProducer = new FakeMessageProducer();

            var header = new MessageHeader(
                messageId: _myRequest.Id, 
                topic: "MyRequest", 
                messageType:MessageType.MT_COMMAND,
                correlationId: _myRequest.ReplyAddress.CorrelationId,
                replyTo: _myRequest.ReplyAddress.Topic);

            var json = new JObject(new JProperty("Id", _myRequest.Id), new JProperty("RequestValue", _myRequest.RequestValue));
            var body = new MessageBody(json.ToString());
            _message = new Message(header, body);
 
            var messageMapperRegistry = new MessageMapperRegistry(new SimpleMessageMapperFactory(() => new MyRequestMessageMapper()));
            messageMapperRegistry.Register<MyRequest, MyRequestMessageMapper>();
            
            var subscriberRegistry = new SubscriberRegistry();
            subscriberRegistry.Register<MyResponse, MyResponseHandler>();
            var handlerFactory = new TestHandlerFactory<MyResponse, MyResponseHandler>(() => new MyResponseHandler());

            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            _commandProcessor = new CommandProcessor(
                subscriberRegistry,
                handlerFactory,
                new InMemoryRequestContextFactory(),
                new PolicyRegistry { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } },
                messageMapperRegistry,
                (IAmAMessageStore<Message>)null,
                (IAmAMessageProducer)_fakeMessageProducer);
  
        }


        [Fact]
        public void When_Calling_A_Server_Via_The_Command_Processor()
        {
            _commandProcessor.Call<MyRequest, MyResponse>(_myRequest, 500);
            
            //should send a message via the messaging gateway
            _fakeMessageProducer.MessageWasSent.Should().BeTrue();

            //should convert the command into a message
            _fakeMessageProducer.SentMessages[0].Should().Be(_message);
            
            //should forward response to a handler
            MyResponseHandler.ShouldReceive(new MyResponse(_myRequest.SendersAddress) {Id = _myRequest.Id});

        }
        
        
        
        public void Dispose()
        {
            _commandProcessor.Dispose();
        }

   }
}
