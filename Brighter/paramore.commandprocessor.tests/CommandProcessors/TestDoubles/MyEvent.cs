using System;
using paramore.brighter.commandprocessor;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyEvent : Event
    {
        public MyEvent()
        {
            Id = Guid.NewGuid();
        }

        public Guid Id { get; private set; }
    }
}