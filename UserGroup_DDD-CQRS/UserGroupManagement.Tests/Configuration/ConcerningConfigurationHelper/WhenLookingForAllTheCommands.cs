using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Machine.Specifications;
using UserGroupManagement.Configuration;
using UserGroupManagement.ServiceLayer.Commands;

namespace UserGroupManagement.Tests.Configuration.ConcerningConfigurationHelper
{
    [Subject(typeof(ConfigurationHelper))]
    public class WhenLookingForAllTheCommands 
    {
        private static readonly ConfigurationHelper configurationHelper = new ConfigurationHelper();
        private static List<Type> commandList;
        private static IEnumerable<Type> foundCommands;

        private Establish context = () =>
        {
            commandList = Assembly.Load("UserGroupManagement.Commands")
                .GetExportedTypes()
                .Where(expT => expT.BaseType == typeof (Command))
                .ToList();
        };

        Because of = () => foundCommands = configurationHelper.FindCommands();

        It should_find_at_least_one_command = () => foundCommands.Count().ShouldBeGreaterThan(0);
        It should_find_all_commands_in_the_same_assembly_as_the_base_command = () => foundCommands.Where(found => commandList.Where(target => target.FullName == found.FullName).Any() == false).Any().ShouldBeFalse();
    }
}
