using System;

namespace Paramore.Brighter.Tests.CommandProcessors.TestDoubles
{
    internal class MyCommandToFail : ICommand
    { 
        public Guid Id { get; set; }
    }
}
