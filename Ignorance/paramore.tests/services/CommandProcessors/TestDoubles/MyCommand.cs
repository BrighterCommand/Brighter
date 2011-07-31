using System;
using Paramore.Services.CommandProcessors;
using Paramore.Services.Common;

namespace Paramore.Tests.services.CommandProcessors.TestDoubles
{
    internal class MyCommand : ICommand, IRequest
    {
        public MyCommand()
        {
            Id = Guid.NewGuid();
        }

        public Guid Id { get; private set; }
    }
}