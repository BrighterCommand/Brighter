using System.Linq;
using NUnit.Framework;
using SpecUnit;
using UserGroupManagement.Configuration;

namespace UserGroupManagement.Tests.Commands
{
    [TestFixture]
    public class ShouldHaveCommandHandlerForCommand
    {
        [Test]
        public void AtLeastOneHandlerForAnyCommand()
        {
            //arrange
            var configurationHelper = new ConfigurationHelper();
            var commands = configurationHelper.FindCommands();
            var commandHandlers = configurationHelper.FindCommandHandlers();

            //assert
            commands.Where(
                command =>
                    commandHandlers
                    .Where(
                    handler => 
                        handler.GetInterfaces().First().GetGenericArguments().First().FullName == command.FullName)
                        .Any()
                    )
                .Any()
                .ShouldBeTrue();
        }
    }
}
