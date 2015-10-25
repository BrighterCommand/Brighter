using System;
using paramore.brighter.commandprocessor;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyLogWritingCommand : Command
    {
        public MyLogWritingCommand() : base(Guid.NewGuid()){}
    }
}