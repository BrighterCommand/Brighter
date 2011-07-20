using System;

namespace Paramore.Infrastructure.Domain
{
    public interface IEntity
    {
        Guid SisoId { get; }
    }
}