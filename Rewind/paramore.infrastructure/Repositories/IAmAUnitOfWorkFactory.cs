namespace Paramore.Adapters.Infrastructure.Repositories
{
    public interface IAmAUnitOfWorkFactory
    {
        IUnitOfWork CreateUnitOfWork();
    }
}