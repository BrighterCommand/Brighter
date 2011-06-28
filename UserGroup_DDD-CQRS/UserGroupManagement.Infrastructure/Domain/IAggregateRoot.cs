namespace UserGroupManagement.Infrastructure.Domain
{
    public interface IAggregateRoot : IEntity
    {
        int Version { get; }
        int Lock(int expectedVersion);
    }
}