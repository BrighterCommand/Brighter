using System;

namespace Paramore.Brighter.InMemory.Tests.Data
{
    public class SimpleCommand : Command
    {
        public string Data => "Test Data";
        
        public SimpleCommand() : base(Guid.NewGuid()) { }
    }
}
