using System;
using paramore.brighter.commandprocessor;

namespace HelloWorld
{
    internal class GreetingCommand : IRequest
    {
        public GreetingCommand(string name)
        {
            Id = Guid.NewGuid();
            Name = name;
        }

        public Guid Id { get; set; }
        public string Name { get; private set; }
    }
}
