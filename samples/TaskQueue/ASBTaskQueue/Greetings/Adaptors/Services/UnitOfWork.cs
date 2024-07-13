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
        private static readonly SemaphoreSlim _transactionSemaphore = new SemaphoreSlim(1, 1);

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
                await _transactionSemaphore.WaitAsync(cancellationToken);
                try
                {
                    if (_transaction == null)
                    {
                        _transaction = await _dataContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
                    }
                }
                finally
                {
                    _transactionSemaphore.Release();
                }
            }
        }

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            if (_transaction != null)
            {
                await _transactionSemaphore.WaitAsync(cancellationToken);
                try
                {
                    await (_transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask);
                    _transaction = null;
                }
                finally
                {
                    _transactionSemaphore.Release();
                }
            }
        }
        public async Task RollbackAsync(CancellationToken cancellationToken)
        {
            if (_transaction != null)
            {
                await _transactionSemaphore.WaitAsync(cancellationToken);
                try
                {
                    await (_transaction?.RollbackAsync(cancellationToken) ?? Task.CompletedTask);
                    _transaction = null;
                }
                finally
                {
                    _transactionSemaphore.Release();
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
