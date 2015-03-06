namespace Paramore.Rewind.Core.Adapters.Repositories
{
    public interface IAmAUnitOfWorkFactory
    {
        IUnitOfWork CreateUnitOfWork();
    }
}