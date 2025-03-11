using System;
using Xunit;
using Paramore.Brighter.ServiceActivator.Ports.Commands;
using Paramore.Brighter.ServiceActivator.Ports.Mappers;

namespace Paramore.Brighter.Core.Tests.ControlBus
{
    public class HearbeatRequestToMessageMapperTests
    {
        private readonly IAmAMessageMapper<HeartbeatRequest> _mapper;
        private Message _message;
        private readonly HeartbeatRequest _request;
        private const string TOPIC = "test.topic";
        private readonly string _correlationId = Guid.NewGuid().ToString();
        private readonly Publication _publication;

        public HearbeatRequestToMessageMapperTests()
        {
            _mapper = new HeartbeatRequestCommandMessageMapper();

            _request = new HeartbeatRequest(new ReplyAddress(TOPIC, _correlationId));
            
            _publication = new Publication { Topic = new RoutingKey(TOPIC) };
        }

        [Fact]
        public void When_mapping_from_a_heartbeat_request_to_a_message()
        {
            _message = _mapper.MapToMessage(_request, _publication);

            //Should serialize the message_type to the header
            Assert.Equal(MessageType.MT_COMMAND, _message.Header.MessageType);
            //Should serialize the message_id to the message body
            Assert.Contains($"\"id\":\"{_request.Id}\"", _message.Body.Value);
            //Should serialize the topic to the message body
            Assert.Equal(TOPIC, _message.Header.ReplyTo);
            //Should serialize the correlation_id to the message body
            Assert.Equal(_correlationId, _message.Header.CorrelationId);
        }
    }
}
