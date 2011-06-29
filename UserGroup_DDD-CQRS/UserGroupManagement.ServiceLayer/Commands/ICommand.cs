using System;
using UserGroupManagement.ServiceLayer.Common;

namespace UserGroupManagement.ServiceLayer.Commands
{
    public interface ICommand : IRequest
    {
        Guid Id { get; }
    }
}