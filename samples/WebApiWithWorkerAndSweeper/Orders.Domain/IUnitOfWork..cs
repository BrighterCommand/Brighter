using System.Data;

namespace Orders.Domain;

public interface IUnitOfWork
{
    Task BeginTransactionAsync(CancellationToken cancellationToken, IsolationLevel isolationLevel = IsolationLevel.Serializable);
    Task CommitAsync(CancellationToken cancellationToken);
    Task RollbackAsync(CancellationToken cancellationToken);
}
