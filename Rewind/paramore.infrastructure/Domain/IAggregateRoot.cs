using Paramore.Infrastructure.Raven;

namespace Paramore.Infrastructure.Domain
{
    public interface IAmAnAggregateRoot<T> : IEntity<T> where T : IAmADataObject
    {
        void Load(T dataObject);
        Version Lock(Version expectedVersion);
        Version Version { get; }
    }
}