namespace UserGroupManagement.Infrastructure.Domain
{
    public interface IRepository<T> where T: IAggregateRoot
    {
    }
}
