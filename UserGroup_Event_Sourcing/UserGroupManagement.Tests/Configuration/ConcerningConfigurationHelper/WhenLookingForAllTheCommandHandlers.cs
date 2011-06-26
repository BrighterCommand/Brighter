using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SpecUnit;
using UserGroupManagement.CommandHandlers;
using UserGroupManagement.Configuration;
using UserGroupManagement.Utility;

namespace UserGroupManagement.Tests.Configuration.ConcerningConfigurationHelper
{
    [Concern(typeof(ConfigurationHelper))]
    [TestFixture]
    public class WhenLookingForAllTheCommandHandlers : ContextSpecification
    {
        private readonly ConfigurationHelper configurationHelper = new ConfigurationHelper();
        private List<Type> commandHandlerList;
        private IEnumerable<Type> foundCommandHandlers;

        protected override void Context()
        {
           commandHandlerList = Assembly.Load("UserGroupManagement.CommandHandlers")
                .GetExportedTypes()
                .Where(
                    expT =>
                    expT.GetInterfaces().Any(
                        exptInterface =>
                        exptInterface.IsGenericType &&
                        exptInterface.GetGenericTypeDefinition() == typeof (IHandleCommands<>)))
                .ToList();
        }

        protected override void Because()
        {
            foundCommandHandlers = configurationHelper.FindCommandHandlers();
        }

        [Test]
        public void ShouldFindAtLeastOneCommandHandler()
        {
            foundCommandHandlers.Count().ShouldBeGreaterThan(0);
        }

        [Test]
        public void ShouldFindAllCommandsInTheSameAssemblyAsTheBaseCommandHandler()
        {
            foundCommandHandlers.Where(found => commandHandlerList.Where(target => target.FullName == found.FullName).Any() == false).Any().ShouldBeFalse();
        }
 
    }
}
