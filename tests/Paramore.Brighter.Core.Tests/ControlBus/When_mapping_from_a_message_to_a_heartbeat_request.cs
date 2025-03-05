using System;
using Xunit;
using Paramore.Brighter.ServiceActivator.Ports.Commands;
using Paramore.Brighter.ServiceActivator.Ports.Mappers;

namespace Paramore.Brighter.Core.Tests.ControlBus
{
    public class HeartbeatMessageToRequestTests
    {
        private readonly IAmAMessageMapper<HeartbeatRequest> _mapper;
        private readonly Message _message;
        private HeartbeatRequest _request;
        private const string TOPIC = "test.topic";
        private readonly string _correlationId = Guid.NewGuid().ToString();
        private readonly string _commandId = Guid.NewGuid().ToString();

        public HeartbeatMessageToRequestTests()
        {
            _mapper = new HeartbeatRequestCommandMessageMapper();
            var messageHeader = new MessageHeader(
                Guid.NewGuid().ToString(),
                new("Heartbeat"),
                MessageType.MT_COMMAND,
                timeStamp: DateTime.UtcNow,
                correlationId:_correlationId,
                replyTo:new RoutingKey(TOPIC));

            var body = String.Format("\"Id\": \"{0}\"", _commandId);
            var messageBody = new MessageBody("{" + body + "}");
            _message = new Message(messageHeader, messageBody);
        }

        [Fact]
        public void When_mapping_from_a_message_to_a_heartbeat_request()
        {
            _request = _mapper.MapToRequest(_message);

 
            // Should put the message reply topic into the address
            Assert.Equal(new RoutingKey(TOPIC), _request.ReplyAddress.Topic);
            // Should put the message correlation id into the address
            Assert.Equal(_correlationId, _request.ReplyAddress.CorrelationId);
            // Should set the id of the request
            Assert.Equal(_commandId, _request.Id);

        }
    }
}
