using System;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Paramore.Brighter.ServiceActivator.TestHelpers;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Polly;
using ServiceStack.DataAnnotations;
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
            var handlerFactory = new TestHandlerFactory<MyResponse, MyResponseHandler>(() => new MyResponseHandler());

            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            InMemoryChannelFactory inMemoryChannelFactory = new InMemoryChannelFactory();
            //we need to seed the response as the fake producer does not actually send across the wire
            inMemoryChannelFactory.SeedChannel(new[] {_message});
            
            _commandProcessor = new CommandProcessor(
                subscriberRegistry,
                handlerFactory,
                new InMemoryRequestContextFactory(),
                new PolicyRegistry { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } },
                messageMapperRegistry,
                (IAmAMessageStore<Message>)null,
                (IAmAMessageProducer)_fakeMessageProducer,
                responseChannelFactory: inMemoryChannelFactory);
  
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
            MyResponseHandler.ShouldReceive(new MyResponse(_myRequest.ReplyAddress) {Id = _myRequest.Id});

        }
        
        
        
        public void Dispose()
        {
            _commandProcessor.Dispose();
        }

   }
}
