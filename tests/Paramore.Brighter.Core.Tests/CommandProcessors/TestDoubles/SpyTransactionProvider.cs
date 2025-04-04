using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;

public class SpyTransactionProvider : IAmABoxTransactionProvider<SpyTransaction>
{
    private SpyTransaction? _transaction;
    private object _lock = new object();

    public bool HasOpenTransaction => _transaction != null;

    public bool IsSharedConnection => false;

    public void Close()
    {
        _transaction = null;
    }

    public void Commit()
    {
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public SpyTransaction GetTransaction()
    {
        if (_transaction == null)
        {
            lock (_lock)
            {
                if (_transaction == null)
                {
                    _transaction = new SpyTransaction();
                }
            }
        }

        return _transaction;
    }

    public Task<SpyTransaction> GetTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetTransaction());
    }

    public void Rollback()
    {
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
