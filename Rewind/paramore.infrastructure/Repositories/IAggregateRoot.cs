namespace Paramore.Adapters.Infrastructure.Repositories
{
    public interface IAmAnAggregateRoot<TDocument> where TDocument : IAmADocument
    {
        Id Id { get; }
        void Load(TDocument document);
        Version Lock(Version expectedVersion);
        TDocument ToDocument();
        Version Version { get; }
    }
}