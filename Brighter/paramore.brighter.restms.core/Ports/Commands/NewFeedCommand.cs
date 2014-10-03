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
        public NewFeedCommand(string name, string type = null, string title = null, string licence = null) : base(Guid.NewGuid())
        {
            Name = name;
            Title = title;
            Licence = licence;
            Type = type;
        }

        public string Name { get; private set; }
        public string Type { get; private set; }
        public string Title { get; private set; }
        public string Licence { get; private set; }
    }
}
