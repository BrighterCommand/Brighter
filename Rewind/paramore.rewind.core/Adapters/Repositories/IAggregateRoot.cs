namespace Paramore.Rewind.Core.Adapters.Repositories
{
    public interface IAmAnAggregateRoot<TDocument> where TDocument : IAmADocument
    {
        Id Id { get; }
        Version Version { get; }
        void Load(TDocument document);
        Version Lock(Version expectedVersion);
        TDocument ToDocument();
    }
}