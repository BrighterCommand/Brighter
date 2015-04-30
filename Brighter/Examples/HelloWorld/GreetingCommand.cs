using System;
using paramore.brighter.commandprocessor;

namespace HelloWorld
{
    internal class GreetingCommand : Command
    {
        public GreetingCommand(string name)
            :base(new Guid())
        {
            Name = name;
        }

        public string Name { get; private set; }
    }
}
