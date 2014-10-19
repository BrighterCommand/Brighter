using System;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.restms.core.Ports.Commands
{
    public class AddJoinToFeedCommand : Command
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Command"/> class.
        /// </summary>
        /// <param name="id">The identifier.</param>
        public AddJoinToFeedCommand(string feedAddress, string addressPattern) : base(Guid.NewGuid())
        {
            FeedAddress = feedAddress;
            AddressPattern = addressPattern;
        }

        public string FeedAddress { get; private set; }
        public string AddressPattern { get; private set; }
    }
}
