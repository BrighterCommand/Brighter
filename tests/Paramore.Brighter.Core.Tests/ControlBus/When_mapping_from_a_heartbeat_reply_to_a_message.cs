using System;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Ports.Commands;
using Paramore.Brighter.ServiceActivator.Ports.Mappers;

namespace Paramore.Brighter.Core.Tests.ControlBus
{
    public class HeartbeatReplyToMessageMapperTests
    {
        private readonly IAmAMessageMapper<HeartbeatReply> _mapper;
        private Message _message;
        private readonly HeartbeatReply _request;
        private const string TOPIC = "test.topic";
        private readonly string _correlationId = Guid.NewGuid().ToString();
        private readonly Publication _publication;
        public HeartbeatReplyToMessageMapperTests()
        {
            _mapper = new HeartbeatReplyCommandMessageMapper();
            _request = new HeartbeatReply("Test.Hostname", new ReplyAddress(TOPIC, _correlationId));
            var firstConsumer = new RunningConsumer(new ConsumerName("Test.Consumer1"), ConsumerState.Open);
            _request.Consumers.Add(firstConsumer);
            var secondConsumer = new RunningConsumer(new ConsumerName("More.Consumers2"), ConsumerState.Shut);
            _request.Consumers.Add(secondConsumer);
            _publication = new Publication
            {
                Topic = new RoutingKey(TOPIC)
            };
        }

        [Test]
        public async Task When_mapping_from_a_heartbeat_reply_to_a_message()
        {
            _message = _mapper.MapToMessage(_request, _publication);
            //Should put the reply to as the topic
            await Assert.That(_message.Header.Topic).IsEqualTo(new RoutingKey(TOPIC));
            //Should put the correlation_id in the header
            await Assert.That(_message.Header.CorrelationId.Value).IsEqualTo(_correlationId);
            // Should put the correlation_id in the reply
            await Assert.That(_request.CorrelationId.ToString()).IsEqualTo(_correlationId);
            // Reply correlation id should be set to the sender's address correlation id
            await Assert.That(Reply.SenderCorrelationIdOrDefault(_request.SendersAddress)).IsEqualTo(_request.CorrelationId);
            //Should put the connections into the body
            await Assert.That(_message.Body.Value).Contains("\"consumerName\":\"Test.Consumer1\"");
            await Assert.That(_message.Body.Value).Contains("\"state\":\"Open");
            await Assert.That(_message.Body.Value).Contains("\"consumerName\":\"More.Consumers2\"");
            await Assert.That(_message.Body.Value).Contains("\"state\":\"Shut");
            //Should put the hostname in the message body
            await Assert.That(_message.Body.Value).Contains("\"hostName\":\"Test.Hostname\"");
        }
    }
}