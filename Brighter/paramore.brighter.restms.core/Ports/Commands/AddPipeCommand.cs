using System;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.restms.core.Ports.Commands
{
    public class AddPipeCommand : Command
    {
        public string DomainName { get; private set; }
        public string Type { get; private set; }
        public string Title { get; private set; }

        public AddPipeCommand(string domainName, string type, string title)
            :base(Guid.NewGuid())
        {
            DomainName = domainName;
            Type = type;
            Title = title;
        }
    }
}
