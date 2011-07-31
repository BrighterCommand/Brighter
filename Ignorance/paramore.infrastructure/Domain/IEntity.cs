using System;

namespace Paramore.Infrastructure.Domain
{
    public interface IEntity
    {
        Id Id { get; }
        Guid SisoId { get; }
    }
}