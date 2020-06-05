using System;
using Paramore.Brighter;

namespace Tests
{
    public class TestEvent : Event
    {
        public TestEvent() : base(Guid.NewGuid())
        {
        }
    }
}