﻿using System;
using Paramore.Brighter;

namespace Greetings.Ports.Events
{
    public class GreetingAsyncEvent : Event
    {
        public GreetingAsyncEvent() : base(Guid.NewGuid()) { }

        public GreetingAsyncEvent(string greeting) : base(Guid.NewGuid())
        {
            Greeting = greeting;
        }

        public string Greeting { get; set; }
    }
}
