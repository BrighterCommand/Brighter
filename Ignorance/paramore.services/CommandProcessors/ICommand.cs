using System;
using Paramore.Services.Common;

namespace Paramore.Services.CommandProcessors
{
    public interface ICommand : IRequest
    {
        Guid Id { get; }
    }
}