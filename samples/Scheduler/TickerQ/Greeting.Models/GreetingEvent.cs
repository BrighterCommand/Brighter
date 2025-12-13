using Paramore.Brighter;

namespace Greeting.Models
{
    public class GreetingEvent : Event
    {
        public GreetingEvent() : base(Id.Random()) { }

        public GreetingEvent(string name) : base(Id.Random())
        {
            Name = name;
        }

        public string Name { get; set; } = string.Empty;
    }
}
