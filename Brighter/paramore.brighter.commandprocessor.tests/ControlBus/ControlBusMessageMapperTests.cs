using System;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.serviceactivator;
using paramore.brighter.serviceactivator.Ports.Commands;
using paramore.brighter.serviceactivator.Ports.Mappers;

namespace paramore.commandprocessor.tests.ControlBus
{
    public class When_mapping_to_a_wire_message_from_a_configuration_command
    {
        private static IAmAMessageMapper<ConfigurationCommand> s_mapper;
        private static Message s_message;
        private static ConfigurationCommand s_command;


        private Establish _context = () =>
        {
            s_mapper = new ConfigurationCommandMessageMapper();

            //"{\"Type\":1,\"ConnectionName\":\"getallthethings\",\"Id\":\"XXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX\"}"
            s_command = new ConfigurationCommand(ConfigurationCommandType.CM_STARTALL) {ConnectionName = "getallthethings"};
        };


        private Because _of = () => s_message = s_mapper.MapToMessage(s_command);

        private It _should_serialize_the_command_type_to_the_message_body = () => s_message.Body.Value.Contains("\"Type\":1").ShouldBeTrue();
        private It _should_serialize_the_message_type_to_the_header = () => s_message.Header.MessageType.ShouldEqual(MessageType.MT_COMMAND); 
        private It _should_serialize_the_connection_name_to_the_message_body =() => s_message.Body.Value.Contains("\"ConnectionName\":\"getallthethings\"").ShouldBeTrue();
        private It _should_serialize_the_message_id_to_the_message_body = () => s_message.Body.Value.Contains(string.Format("\"Id\":\"{0}\"", s_command.Id)).ShouldBeTrue();
    }

    public class When_mapping_from_a_configuration_command_from_a_message
    {
        private static IAmAMessageMapper<ConfigurationCommand> s_mapper;
        private static Message s_message;
        private static ConfigurationCommand s_command;

        private Establish _context = () =>
        {
            s_mapper = new ConfigurationCommandMessageMapper();

            s_message = new Message(
                new MessageHeader(Guid.NewGuid(), "myTopic", MessageType.MT_COMMAND), 
                new MessageBody(string.Format("{{\"Type\":1,\"ConnectionName\":\"getallthethings\",\"Id\":\"{0}\"}}", Guid.NewGuid()))
                );
        };

        private Because _of = () => s_command = s_mapper.MapToRequest(s_message);

        private It _should_rehydrate_the_command_type = () => s_command.Type.ShouldEqual(ConfigurationCommandType.CM_STARTALL);
        private It _should_rehydrate_the_connection_name = () => s_command.ConnectionName.ShouldEqual("getallthethings");
    }

    public class When_mapping_from_a_heartbeat_request_to_a_message
    {
        private static IAmAMessageMapper<HeartbeatRequest> s_mapper;
        private static Message s_message;
        private static HeartbeatRequest s_request;
        private const string TOPIC = "test.topic";
        private static readonly Guid s_correlationId = Guid.NewGuid();

        private Establish _context = () =>
        {
            s_mapper = new HeartbeatRequestCommandMessageMapper();

            s_request = new HeartbeatRequest(new ReplyAddress(TOPIC, s_correlationId));
        };

        private Because _of = () => s_message = s_mapper.MapToMessage(s_request);

        private It _should_serialize_the_message_type_to_the_header = () => s_message.Header.MessageType.ShouldEqual(MessageType.MT_COMMAND); 
        private It _should_serialize_the_message_id_to_the_message_body = () => s_message.Body.Value.Contains(string.Format("\"Id\": \"{0}\"", s_request.Id)).ShouldBeTrue();
        private It _should_serialize_the_topic_to_the_message_body = () => s_message.Header.ReplyTo.ShouldEqual(TOPIC);
        private It _should_serialize_the_correlation_id_to_the_message_body = () => s_message.Header.CorrelationId.ShouldEqual(s_correlationId);

    }

    public class When_mapping_from_a_message_to_a_heartbeat_request
    {
        private static IAmAMessageMapper<HeartbeatRequest> s_mapper;
        private static Message s_message;
        private static HeartbeatRequest s_request;
        private const string TOPIC = "test.topic";
        private static readonly Guid s_correlationId = Guid.NewGuid();
        private static readonly Guid s_commandId = Guid.NewGuid();

        private Establish _context = () =>
        {
            s_mapper = new HeartbeatRequestCommandMessageMapper();
            var messageHeader = new MessageHeader(
                messageId: Guid.NewGuid(),
                topic: "Heartbeat",
                messageType: MessageType.MT_COMMAND,
                timeStamp: DateTime.UtcNow,
                correlationId: s_correlationId, replyTo: TOPIC);

            var body = String.Format("\"Id\": \"{0}\"", s_commandId);
            var messageBody = new MessageBody("{" + body + "}");
            s_message = new Message(header: messageHeader, body: messageBody);
        };

        private Because _of = () => s_request = s_mapper.MapToRequest(s_message);

        private It _should_put_the_message_reply_topic_into_the_address = () => s_request.ReplyAddress.Topic.ShouldEqual(TOPIC);
        private It _should_put_the_message_correlation_id_into_the_address = () => s_request.ReplyAddress.CorrelationId.ShouldEqual(s_correlationId);
        private It _should_set_the_id_of_the_request = () => s_request.Id.ShouldEqual(s_message.Id);

    }

    public class When_mapping_from_a_heartbeat_reply_to_a_message
    {
        private static IAmAMessageMapper<HeartbeatReply> s_mapper;
        private static Message s_message;
        private static HeartbeatReply s_request;
        private const string TOPIC = "test.topic";
        private static readonly Guid s_correlationId = Guid.NewGuid();
        private static RunningConsumer s_firstConsumer;
        private static RunningConsumer s_secondConsumer;

        private Establish _context = () =>
        {
            s_mapper = new HeartbeatReplyMessageMapper();
            s_request = new HeartbeatReply("Test.Hostname", new ReplyAddress(TOPIC, s_correlationId));
            s_firstConsumer = new RunningConsumer(new ConnectionName("Test.Connection"), ConsumerState.Open);
            s_request.Consumers.Add(s_firstConsumer);
            s_secondConsumer = new RunningConsumer(new ConnectionName("More.Consumers"),ConsumerState.Shut );
            s_request.Consumers.Add(s_secondConsumer);
        };

        private Because _of = () => s_message = s_mapper.MapToMessage(s_request);

        private It _should_put_the_reply_to_as_the_topic = () => s_message.Header.Topic.ShouldEqual(TOPIC);
        private It _should_put_the_correlation_id_in_the_header = () => s_message.Header.CorrelationId.ShouldEqual(s_correlationId);
        private It _should_put_the_connections_into_the_body = () =>
        {
            s_message.Body.ShouldMatch(body => body.Value.Contains("\"ConnectionName\": \"Test.Connection\""));
            s_message.Body.ShouldMatch(body => body.Value.Contains("\"State\": 1"));
            s_message.Body.ShouldMatch(body => body.Value.Contains("\"ConnectionName\": \"More.Consumers\""));
            s_message.Body.ShouldMatch(body => body.Value.Contains("\"State\": 0"));
        };

        private It _should_put_the_hostname_in_the_message_body = () => s_message.Body.ShouldMatch(body => body.Value.Contains("\"HostName\": \"Test.Hostname\""));
    }

    public class When_maping_from_a_message_to_a_heartbeat_reply
    {
        private static IAmAMessageMapper<HeartbeatReply> s_mapper;
        private static Message s_message;
        private static HeartbeatReply s_request;
        private const string MESSAGE_BODY = "{\r\n  \"HostName\": \"Test.Hostname\",\r\n  \"Consumers\": [\r\n    {\r\n      \"ConnectionName\": \"Test.Connection\",\r\n      \"State\": 1\r\n    },\r\n    {\r\n      \"ConnectionName\": \"More.Consumers\",\r\n      \"State\": 0\r\n    }\r\n  ]\r\n}";
        private const string TOPIC = "test.topic";
        private static readonly Guid s_correlationId = Guid.NewGuid();

        private Establish _context = () =>
        {
            s_mapper = new HeartbeatReplyMessageMapper();
            var header = new MessageHeader(messageId: Guid.NewGuid(), topic: TOPIC, messageType: MessageType.MT_COMMAND, timeStamp: DateTime.UtcNow, correlationId: s_correlationId);
            var body = new MessageBody(MESSAGE_BODY);
            s_message = new Message(header, body);
        };

        private Because _of = () => s_request = s_mapper.MapToRequest(s_message);

        private It _should_set_the_sender_address_topic = () => s_request.SendersAddress.Topic.ShouldEqual(TOPIC);
        private It _should_set_the_sender_correlation_id = () => s_request.SendersAddress.CorrelationId.ShouldEqual(s_correlationId);
        private It _should_set_the_hostName = () => s_request.HostName.ShouldEqual("Test.Hostname");
        private It _should_contain_the_consumers = () =>
        {
            s_request.Consumers.ShouldContain(rc => rc.ConnectionName == "Test.Connection" && rc.State == ConsumerState.Open);
            s_request.Consumers.ShouldContain(rc => rc.ConnectionName == "More.Consumers" && rc.State == ConsumerState.Shut);
        };


    }
}
