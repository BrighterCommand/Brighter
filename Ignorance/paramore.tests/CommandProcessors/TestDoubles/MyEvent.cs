using System;
using Paramore.Services.Events;

namespace Paramore.Tests.CommandProcessors.TestDoubles
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