using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace Paramore.Brighter
{
    public class CommittableTransactionProvider : IAmABoxTransactionProvider<CommittableTransaction>
    {
        private CommittableTransaction? _transaction;
        private Transaction? _existingTransaction;

        public void Close()
        {
            Transaction.Current = _existingTransaction;
            _transaction = null;
        }

        public void Commit()
        {
            _transaction?.Commit();
            Close();
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction is null)
                return Task.CompletedTask;
            return Task.Factory.FromAsync(_transaction.BeginCommit, _transaction.EndCommit, null, TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public CommittableTransaction GetTransaction()
        {
            if (_transaction == null)
            {
                _existingTransaction = Transaction.Current;
                _transaction = new CommittableTransaction();
                Transaction.Current = _transaction;
            }
            return _transaction;
        }

        public Task<CommittableTransaction> GetTransactionAsync(CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<CommittableTransaction>(TaskCreationOptions.RunContinuationsAsynchronously);
            tcs.SetResult(GetTransaction());
            return tcs.Task;
        }

        public bool HasOpenTransaction { get { return _transaction != null; } }
        public bool IsSharedConnection => true;
        
        public void Rollback()
        {
            _transaction?.Rollback();
            Close();
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Rollback();
            return Task.CompletedTask;
        }
    }
}
