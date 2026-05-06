using System;
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
            _publication = new Publication
            {
                Topic = new RoutingKey(TOPIC)
            };
        }

        [Test]
        public async Task When_mapping_from_a_heartbeat_request_to_a_message()
        {
            _message = _mapper.MapToMessage(_request, _publication);
            //Should serialize the message_type to the header
            await Assert.That(_message.Header.MessageType).IsEqualTo(MessageType.MT_COMMAND);
            //Should serialize the message_id to the message body
            await Assert.That(_message.Body.Value).Contains($"\"id\":\"{_request.Id}\"");
            //Should serialize the topic to the message body
            await Assert.That(_message.Header.ReplyTo?.Value).IsEqualTo(TOPIC);
            //Should serialize the correlation_id to the message body
            await Assert.That(_message.Header.CorrelationId.Value).IsEqualTo(_correlationId);
        }
    }
}