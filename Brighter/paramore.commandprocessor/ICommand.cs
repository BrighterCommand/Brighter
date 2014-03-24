using System;

namespace paramore.brighter.commandprocessor
{
    public interface ICommand : IRequest
    {
        Guid Id { get; }
    }
}