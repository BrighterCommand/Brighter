using System;
using Xunit;
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
            
            var secondConsumer = new RunningConsumer(new ConsumerName("More.Consumers2"),ConsumerState.Shut );
            _request.Consumers.Add(secondConsumer);
            
            _publication = new Publication { Topic = new RoutingKey(TOPIC) };
        }

        [Fact]
        public void When_mapping_from_a_heartbeat_reply_to_a_message()
        {
            _message = _mapper.MapToMessage(_request, _publication);

            //Should put the reply to as the topic
            Assert.Equal(new RoutingKey(TOPIC), _message.Header.Topic);
            
            //Should put the correlation_id in the header
            Assert.Equal(_correlationId, _message.Header.CorrelationId);

            // Should put the correlation_id in the reply
            Assert.Equal(_correlationId, _request.CorrelationId.ToString());

            // Reply correlation id should be set to the sender's address correlation id
            Assert.Equal(_request.CorrelationId, Reply.SenderCorrelationIdOrDefault(_request.SendersAddress));

            //Should put the connections into the body
            Assert.Contains("\"consumerName\":\"Test.Consumer1\"", _message.Body.Value);
            Assert.Contains("\"state\":\"Open", _message.Body.Value);
            Assert.Contains("\"consumerName\":\"More.Consumers2\"", _message.Body.Value);
            Assert.Contains("\"state\":\"Shut", _message.Body.Value);
            

            //Should put the hostname in the message body
            Assert.Contains("\"hostName\":\"Test.Hostname\"", _message.Body.Value);
        }
    }
}
