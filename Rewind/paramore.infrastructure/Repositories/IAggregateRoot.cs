namespace Paramore.Infrastructure.Repositories
{
    public interface IAmAnAggregateRoot<T> where T : IAmADocument
    {
        Id Id { get; }
        void Load(T document);
        Version Lock(Version expectedVersion);
        Version Version { get; }
    }
}