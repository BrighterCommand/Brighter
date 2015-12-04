using System;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.serviceactivator.Ports.Commands;
using paramore.brighter.serviceactivator.Ports.Mappers;

namespace paramore.commandprocessor.tests.ControlBus
{
    public class When_mapping_to_a_wire_message_from_a_configuration_command
    {
        private static IAmAMessageMapper<ConfigurationCommand> s_mapper;
        private static Message s_message;
        private static ConfigurationCommand s_command;


        private Establish context = () =>
        {
            s_mapper = new ConfigurationCommandMessageMapper();

            //"{\"Type\":1,\"ConnectionName\":\"getallthethings\",\"Id\":\"XXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX\"}"
            s_command = new ConfigurationCommand(ConfigurationCommandType.CM_STARTALL) {ConnectionName = "getallthethings"};
        };


        private Because of = () => s_message = s_mapper.MapToMessage(s_command);

        private It should_serialize_the_command_type_to_the_message_body = () => s_message.Body.Value.Contains("\"Type\":1").ShouldBeTrue();
        private It should_serialize_the_message_type_to_the_header = () => s_message.Header.MessageType.ShouldEqual(MessageType.MT_COMMAND); 
        private It should_serialize_the_connection_name_to_the_message_body =() => s_message.Body.Value.Contains("\"ConnectionName\":\"getallthethings\"").ShouldBeTrue();
        private It should_serialize_the_message_id_to_the_message_body = () => s_message.Body.Value.Contains(string.Format("\"Id\":\"{0}\"", s_command.Id)).ShouldBeTrue();
    }

    public class When_mapping_from_a_configuration_command_from_a_message
    {
        private static IAmAMessageMapper<ConfigurationCommand> s_mapper;
        private static Message s_message;
        private static ConfigurationCommand s_command;

        private Establish context = () =>
        {
            s_mapper = new ConfigurationCommandMessageMapper();

            s_message = new Message(
                new MessageHeader(Guid.NewGuid(), "myTopic", MessageType.MT_COMMAND), 
                new MessageBody(string.Format("{{\"Type\":1,\"ConnectionName\":\"getallthethings\",\"Id\":\"{0}\"}}", Guid.NewGuid()))
                );
        };

        private Because of = () => s_command = s_mapper.MapToRequest(s_message);

        private It should_rehydrate_the_command_type = () => s_command.Type.ShouldEqual(ConfigurationCommandType.CM_STARTALL);
        private It should_rehydrate_the_connection_name = () => s_command.ConnectionName.ShouldEqual("getallthethings");
    }

    public class When_mapping_from_a_heartbeat_command_to_a_message
    {
        private static IAmAMessageMapper<HeartbeatCommand> s_mapper;
        private static Message s_message;
        private static HeartbeatCommand s_command;
        private const string TOPIC = "test.topic";
        private static readonly Guid s_correlationId = Guid.NewGuid();

        private Establish context = () =>
        {
            s_mapper = new HeartbeatCommandMessageMapper();

            s_command = new HeartbeatCommand();
        };

        private Because of = () => s_message = s_mapper.MapToMessage(s_command);

        private It should_serialize_the_message_type_to_the_header = () => s_message.Header.MessageType.ShouldEqual(MessageType.MT_COMMAND); 
        private It should_serialize_the_message_id_to_the_message_body = () => s_message.Body.Value.Contains(string.Format("\"Id\":\"{0}\"", s_command.Id)).ShouldBeTrue();

    }
}
