using System;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Ports.Commands;
using Paramore.Brighter.ServiceActivator.Ports.Mappers;

namespace Paramore.Brighter.Core.Tests.ControlBus
{
    public class HeartbeatMessageToReplyTests
    {
        private readonly IAmAMessageMapper<HeartbeatReply> _mapper;
        private readonly Message _message;
        private HeartbeatReply _request;
        private const string MESSAGE_BODY = "{\r\n  \"HostName\": \"Test.Hostname\",\r\n  \"Consumers\": [\r\n    {\r\n      \"ConsumerName\": \"Test.Subscription\",\r\n      \"State\": 1\r\n    },\r\n    {\r\n      \"ConsumerName\": \"More.Consumers\",\r\n      \"State\": 0\r\n    }\r\n  ]\r\n}";
        private readonly RoutingKey _routingKey = new("test.topic");
        private readonly string _correlationId = Guid.NewGuid().ToString();

        public HeartbeatMessageToReplyTests()
        {
            _mapper = new HeartbeatReplyCommandMessageMapper();
            var header = new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND, 
                timeStamp: DateTime.UtcNow, correlationId:_correlationId
            );
            var body = new MessageBody(MESSAGE_BODY);
            _message = new Message(header, body);
        }

        [Fact]
        public void When_mapping_from_a_message_to_a_heartbeat_reply()
        {
            _request = _mapper.MapToRequest(_message);

            // Should set the sender address topic
            Assert.Equal(_routingKey, _request.SendersAddress.Topic);
            // Should set the sender correlation_id
            Assert.Equal(_correlationId, _request.SendersAddress.CorrelationId);

            // Reply should have the same correlation id as the original message
            Assert.NotEqual(Reply.SenderCorrelationIdOrDefault(_request.SendersAddress), Id.Empty);
            Assert.Equal(_request.CorrelationId, Reply.SenderCorrelationIdOrDefault(_request.SendersAddress));

            // Should set the hostName
            Assert.Equal("Test.Hostname", _request.HostName);
            // Should contain the consumers
            Assert.Contains(_request.Consumers, rc => rc.ConsumerName == "Test.Subscription" && rc.State == ConsumerState.Open);
            Assert.Contains(_request.Consumers, rc => rc.ConsumerName == "More.Consumers" && rc.State == ConsumerState.Shut);
        }
   }
}
