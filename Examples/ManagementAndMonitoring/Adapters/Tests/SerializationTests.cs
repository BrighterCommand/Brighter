//TODO: .NetCore mspec is required
//using Machine.Specifications;
//using ManagementAndMonitoring.Ports.Commands;
//using ManagementAndMonitoring.Ports.Mappers;
//using paramore.brighter.commandprocessor;

//namespace ManagementAndMonitoring.Adapters.Tests
//{
//    public class When_Serializing_to_and_from_a_greeting
//    {
//        private static GreetingCommandMessageMapper s_greetingCommandMessageMapper;
//        private static GreetingCommand s_command;
//        private static Message s_message;
//        private const string GREETING = "Hello World";

//        private Establish context = () =>
//        {
//            var command = new GreetingCommand(GREETING);
//            s_greetingCommandMessageMapper = new GreetingCommandMessageMapper();
//            s_message = s_greetingCommandMessageMapper.MapToMessage(command);
//        };

//        private Because of = () => s_command = s_greetingCommandMessageMapper.MapToRequest(s_message);

//        private It _should_have_the_correct_greeting = () => s_command.Greeting.ShouldEqual(GREETING);
//    }
//}
