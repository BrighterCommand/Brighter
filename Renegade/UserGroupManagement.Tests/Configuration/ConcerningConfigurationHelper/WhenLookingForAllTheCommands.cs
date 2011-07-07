using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SpecUnit;
using UserGroupManagement.Commands;
using UserGroupManagement.Configuration;

namespace UserGroupManagement.Tests.Configuration.ConcerningConfigurationHelper
{
    [Concern(typeof(ConfigurationHelper))]
    [TestFixture]
    public class WhenLookingForAllTheCommands : ContextSpecification
    {
        private readonly ConfigurationHelper configurationHelper = new ConfigurationHelper();
        private List<Type> commandList;
        private IEnumerable<Type> foundCommands;

        protected override void Context()
        {
            commandList = Assembly.Load("UserGroupManagement.Commands")
                .GetExportedTypes()
                .Where(expT => expT.BaseType == typeof (Command))
                .ToList();
        }

        protected override void Because()
        {
            foundCommands = configurationHelper.FindCommands();
        }

        [Test]
        public void ShouldFindAtLeastOneCommand()
        {
            foundCommands.Count().ShouldBeGreaterThan(0);
        }

        [Test]
        public void ShouldFindAllCommandsInTheSameAssemblyAsTheBaseCommand()
        {
            foundCommands.Where(found => commandList.Where(target => target.FullName == found.FullName).Any() == false).Any().ShouldBeFalse();
        }
    }
}
