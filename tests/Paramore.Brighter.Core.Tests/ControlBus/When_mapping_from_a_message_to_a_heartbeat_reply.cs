using System;
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
            var header = new MessageHeader(Guid.NewGuid().ToString(), _routingKey, MessageType.MT_COMMAND, timeStamp: DateTime.UtcNow, correlationId: _correlationId);
            var body = new MessageBody(MESSAGE_BODY);
            _message = new Message(header, body);
        }

        [Test]
        public async Task When_mapping_from_a_message_to_a_heartbeat_reply()
        {
            _request = _mapper.MapToRequest(_message);
            // Should set the sender address topic
            await Assert.That(_request.SendersAddress.Topic).IsEqualTo(_routingKey);
            // Should set the sender correlation_id
            await Assert.That(_request.SendersAddress.CorrelationId.Value).IsEqualTo(_correlationId);
            // Reply should have the same correlation id as the original message
            await Assert.That(Id.Empty).IsNotEqualTo(Reply.SenderCorrelationIdOrDefault(_request.SendersAddress));
            await Assert.That(Reply.SenderCorrelationIdOrDefault(_request.SendersAddress)).IsEqualTo(_request.CorrelationId);
            // Should set the hostName
            await Assert.That(_request.HostName).IsEqualTo("Test.Hostname");
            // Should contain the consumers
            await Assert.That(_request.Consumers).Contains(rc => rc.ConsumerName == "Test.Subscription" && rc.State == ConsumerState.Open);
            await Assert.That(_request.Consumers).Contains(rc => rc.ConsumerName == "More.Consumers" && rc.State == ConsumerState.Shut);
        }
    }
}