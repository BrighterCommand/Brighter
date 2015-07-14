using Greetings.Ports.Commands;
using Greetings.Ports.Mappers;
using Machine.Specifications;
using paramore.brighter.commandprocessor;

namespace Greetings.Adapters.Tests
{
    public class When_Serializing_to_and_from_a_greeting
    {
        private static GreetingCommandMessageMapper s_greetingCommandMessageMapper;
        private static GreetingCommand s_command;
        private static Message s_message;
        private const string GREETING = "Hello World";

        private Establish context = () =>
        {
            var command = new GreetingCommand(GREETING);
            s_greetingCommandMessageMapper = new GreetingCommandMessageMapper();
            s_message = s_greetingCommandMessageMapper.MapToMessage(command);
        };

        private Because of = () => s_command = s_greetingCommandMessageMapper.MapToRequest(s_message);

        private It _should_have_the_correct_greeting = () => s_command.Greeting.ShouldEqual(GREETING);
    }
}
