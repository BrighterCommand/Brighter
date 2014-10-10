using System;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.restms.core.Ports.Commands
{
    public class RemoveFeedFromDomainCommand : Command
    {
        public string FeedName { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Command"/> class.
        /// </summary>
        /// <param name="feedName"></param>
        /// <param name="id">The identifier.</param>
        public RemoveFeedFromDomainCommand(string feedName) : base(Guid.NewGuid())
        {
            FeedName = feedName;
        }
    }
}
