using System;

namespace UserGroupManagement.ServiceLayer.Commands
{
    public interface ICommand
    {
        Guid Id { get; }
    }
}