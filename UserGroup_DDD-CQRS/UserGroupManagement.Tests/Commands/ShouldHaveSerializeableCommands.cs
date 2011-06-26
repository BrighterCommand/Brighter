using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SpecUnit;
using UserGroupManagement.Configuration;
using UserGroupManagement.Utility;

namespace UserGroupManagement.Tests.Commands
{
    [TestFixture]
    public class ShouldHaveSerializeableCommands : ContextSpecification
    {
       [Test]
       public void AnyCommandShouldBeSerializable()
       {
           //arrange
           var commands = new ConfigurationHelper().FindCommands();

           //assert
           commands.Each(
               command => 
                   command.GetCustomAttributes(false)
                   .Where(attribute => attribute.GetType() == typeof(SerializableAttribute))
                   .Any()
                   .ShouldBeTrue()
                   );
       }
    }
}
