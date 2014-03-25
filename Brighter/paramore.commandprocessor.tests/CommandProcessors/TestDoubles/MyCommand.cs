using System;
using paramore.brighter.commandprocessor;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal class MyCommand : ICommand, IRequest
    {
        public MyCommand()
        {
            Id = Guid.NewGuid();
        }

        public Guid Id { get; private set; }

        public string Value { get; set; }
    }
}