using System;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.restms.core.Ports.Commands
{
    public class NewFeedCommand : Command
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Command"/> class.
        /// </summary>
        /// <param name="id">The identifier.</param>
        public NewFeedCommand(string domainName, string name, string type = null, string title = null, string license = null) : base(Guid.NewGuid())
        {
            DomainName = domainName;
            Name = name;
            Title = title;
            License = license;
            Type = type;
        }

        public string DomainName { get; set; }
        public string Name { get; private set; }
        public string Type { get; private set; }
        public string Title { get; private set; }
        public string License { get; private set; }
    }
}
