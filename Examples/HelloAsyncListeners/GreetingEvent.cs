using System;
using paramore.brighter.commandprocessor;

namespace HelloAsyncListeners
{
    public class GreetingEvent : Event
    {
        public GreetingEvent(string name) : base(new Guid())
        {
            Name = name;
        }

        public string Name { get; private set; }
    }
}
