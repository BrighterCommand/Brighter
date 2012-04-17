namespace Paramore.Infrastructure.Repositories
{
    public interface IAmAUnitOfWorkFactory
    {
        IUnitOfWork CreateUnitOfWork();
    }
}