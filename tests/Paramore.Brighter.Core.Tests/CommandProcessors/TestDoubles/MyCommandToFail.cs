using System;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    internal class MyCommandToFail : ICommand
    { 
        public Guid Id { get; set; }
    }
}
