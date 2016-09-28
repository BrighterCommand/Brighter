using System;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles
{
    internal class MyLogWritingCommand : Command
    {
        public MyLogWritingCommand() : base(Guid.NewGuid()){}
    }
}