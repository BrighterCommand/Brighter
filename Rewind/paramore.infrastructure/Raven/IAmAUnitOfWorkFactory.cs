using Paramore.Infrastructure.Domain;

namespace Paramore.Infrastructure.Raven
{
    public interface IAmAUnitOfWorkFactory
    {
        IUnitOfWork CreateUnitOfWork();
    }
}