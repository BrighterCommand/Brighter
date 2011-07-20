using System;
using Paramore.Services.Common;

namespace Paramore.Services.Commands
{
    public interface ICommand : IRequest
    {
        Guid Id { get; }
    }
}