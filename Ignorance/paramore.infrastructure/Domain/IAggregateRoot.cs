using Paramore.Infrastructure.Raven;

namespace Paramore.Infrastructure.Domain
{
    public interface IAmAnAggregateRoot<out T> : IEntity<T> where T : IAmADataObject
    {
        Version Lock(Version expectedVersion);
        Version Version { get; }
    }
}