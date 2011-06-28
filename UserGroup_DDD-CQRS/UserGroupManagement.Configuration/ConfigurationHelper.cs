using System;
using System.Collections.Generic;
using System.Linq;
using UserGroupManagement.ServiceLayer.CommandHandlers;
using UserGroupManagement.ServiceLayer.Commands;

namespace UserGroupManagement.Configuration
{
    public class ConfigurationHelper
    {
        public IEnumerable<Type> FindCommands()
        {
            return typeof(Command)
                .Assembly
                .GetExportedTypes()
                .Where(expT => expT.BaseType == typeof(Command))
                .ToList();
        }

        public IEnumerable<Type> FindCommandHandlers()
        {
            return typeof(IHandleCommands<>)
               .Assembly
               .GetExportedTypes()
                .Where(
                    expT =>
                    expT.GetInterfaces().Any(
                        exptInterface =>
                        exptInterface.IsGenericType &&
                        exptInterface.GetGenericTypeDefinition() == typeof(IHandleCommands<>)))
                .ToList();
 
        }
    }
}