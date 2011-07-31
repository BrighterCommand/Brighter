using System;
using Version = Paramore.Infrastructure.Domain.Version;

namespace Paramore.Infrastructure.Domain
{
    public interface IAggregateRoot : IEntity
    {
        Version Version { get; }
        Version Lock(Version expectedVersion);
    }
}