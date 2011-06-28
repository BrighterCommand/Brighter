using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Machine.Specifications;
using NUnit.Framework;
using UserGroupManagement.Configuration;
using UserGroupManagement.ServiceLayer.CommandHandlers;

namespace UserGroupManagement.Tests.Configuration.ConcerningConfigurationHelper
{
    [Subject(typeof(ConfigurationHelper))]
    public class WhenLookingForAllTheCommandHandlers
    {
        private static readonly ConfigurationHelper configurationHelper = new ConfigurationHelper();
        private static List<Type> commandHandlerList;
        private static IEnumerable<Type> foundCommandHandlers;

        Establish context = () =>
        {
            commandHandlerList = Assembly.Load("UserGroupManagement.CommandHandlers")
                .GetExportedTypes()
                .Where(
                    expT =>
                    expT.GetInterfaces().Any(
                        exptInterface =>
                        exptInterface.IsGenericType &&
                        exptInterface.GetGenericTypeDefinition() ==
                        typeof (IHandleCommands<>)))
                .ToList();
        };

        Because of = () =>
        {
            foundCommandHandlers = configurationHelper.FindCommandHandlers();
        };

        It should_find_at_least_one_command_handler = () => foundCommandHandlers.Count().ShouldBeGreaterThan(0); 
        It should_find_all_commands_in_the_same_assembly_as_the_base_command_handler = () => 
            foundCommandHandlers.Where(found => commandHandlerList.Where(target => target.FullName == found.FullName).Any() == false).Any().ShouldBeFalse();
    }
}
