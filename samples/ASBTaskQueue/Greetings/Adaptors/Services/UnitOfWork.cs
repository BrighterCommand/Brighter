using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Greetings.Adaptors.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Greetings.Adaptors.Services
{
    public class UnitOfWork : IUnitOfWork
    {
        private static readonly SemaphoreSlim _transactionSempahore = new SemaphoreSlim(1, 1);

        private readonly GreetingsDataContext _dataContext;
        private IDbContextTransaction _transaction;

        public UnitOfWork(GreetingsDataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public async Task BeginTransactionAsync(CancellationToken cancellationToken, IsolationLevel isolationLevel = IsolationLevel.Serializable)
        {
            // Semaphore is implemented to protected against potential threading issues.
            if (_transaction == null)
            {
                await _transactionSempahore.WaitAsync(cancellationToken);
                try
                {
                    if (_transaction == null)
                    {
                        _transaction = await _dataContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
                    }
                }
                finally
                {
                    _transactionSempahore.Release();
                }
            }
        }

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            if (_transaction != null)
            {
                await _transactionSempahore.WaitAsync(cancellationToken);
                try
                {
                    _transaction?.Commit();
                    _transaction = null;
                }
                finally
                {
                    _transactionSempahore.Release();
                }
            }
        }
        public async Task RollbackAsync(CancellationToken cancellationToken)
        {
            if (_transaction != null)
            {
                await _transactionSempahore.WaitAsync(cancellationToken);
                try
                {
                    _transaction?.Rollback();
                    _transaction = null;
                }
                finally
                {
                    _transactionSempahore.Release();
                }
            }
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _dataContext?.Dispose();
        }
    }
}
