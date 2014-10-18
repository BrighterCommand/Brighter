using System;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.restms.core.Ports.Commands
{
    public class AddPipeToDomainCommand : Command
    {
        public string DomainName { get; private set; }
        public string PipeName { get; private set; }

        public AddPipeToDomainCommand(string domainName, string pipeName) : base(Guid.NewGuid())
        {
            DomainName = domainName;
            PipeName = pipeName;
        }
    }
}
