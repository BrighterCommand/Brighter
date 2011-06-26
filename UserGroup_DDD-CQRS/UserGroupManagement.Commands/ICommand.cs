using System;

namespace UserGroupManagement.Commands
{
    public interface ICommand
    {
        Guid Id { get; }
    }
}