using System;
using Paramore.Brighter;

namespace HelloAsyncListeners
{
    public class GreetingEvent : Event
    {
        public GreetingEvent(string name) : base(Guid.NewGuid())
        {
            Name = name;
        }

        public string Name { get; }
    }
}
