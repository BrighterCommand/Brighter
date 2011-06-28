using System;

namespace UserGroupManagement.Infrastructure.Domain
{
    public interface IEntity
    {
        Guid Id { get; }
    }
}