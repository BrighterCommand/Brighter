﻿using System;

namespace Paramore.Brighter.AWS.Tests.TestDoubles
{
    internal class MyDeferredCommand : Command
    {
        public string Value { get; set; }
        public MyDeferredCommand() : base(Guid.NewGuid()) { }
        
    }
}
