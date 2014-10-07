using System;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.restms.core.Ports.Commands
{
    public class AddFeedToDomainCommand : Command
    {
        public string DomainName { get; private set; }
        public string FeedName { get; private set; }

        public AddFeedToDomainCommand(string domainName, string feedName)
            : base(Guid.NewGuid())
        {
            DomainName = domainName;
            FeedName = feedName;
        }
    }
}