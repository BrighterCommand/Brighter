using System;

namespace paramore.commandprocessor
{
    public interface ICommand : IRequest
    {
        Guid Id { get; }
    }
}