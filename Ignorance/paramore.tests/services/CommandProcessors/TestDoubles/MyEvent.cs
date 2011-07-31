using System;
using Paramore.Services.CommandProcessors;

namespace Paramore.Tests.services.CommandProcessors.TestDoubles
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