using System.Diagnostics;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    internal sealed class MyCommandToFail : ICommand
    { 
        public Id Id { get; set; }
    }
}
