using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    public abstract class RelationalDbConnectionProvider : IAmARelationalDbConnectionProvider
    {
        private bool _disposed = false;
        
        /// <summary>
        /// debugging
        /// </summary>
        public Guid Instance = Uuid.New();

        /// <summary>
        /// Does not retain shared connections or transactions, so nothing to commit
        /// </summary>
        public virtual void Close() { }
        
        /// <summary>
        /// Does not support shared transactions, so nothing to commit, manage transactions independently
        /// </summary>
        public void Commit() { }
        
        /// <summary>
        /// Does not support shared transactions, so nothing to commit, manage transactions independently
        /// </summary>
        public Task CommitAsync(CancellationToken cancellationToken) { return Task.CompletedTask; }
        
        /// <summary>
        /// Gets a existing Connection; creates a new one if it does not exist
        /// Opens the connection if it is not opened
        /// This is not a shared connection
        /// </summary>
        /// <returns>A database connection</returns>
        public abstract DbConnection GetConnection();
        
        /// <summary>
        /// Gets a existing Connection; creates a new one if it does not exist
        /// The connection is not opened, you need to open it yourself.
        /// The base class just returns a new or existing connection, but derived types may perform async i/o
        /// </summary>
        /// <returns>A database connection</returns>
        public virtual Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<DbConnection>();
            tcs.SetResult(GetConnection());
            return tcs.Task;
        }

        /// <summary>
        /// Does not support shared transactions, create a transaction of the DbConnection instead
        /// </summary>
        /// <returns>A database transaction</returns>
        public virtual DbTransaction? GetTransaction() { return null; }
        
        /// <summary>
        /// Does not support shared transactions, create a transaction of the DbConnection instead
        /// </summary>
        /// <returns>A database transaction</returns>
        public virtual Task<DbTransaction>? GetTransactionAsync(CancellationToken cancellationToken = default)
        {
            return null;
        }

        /// <summary>
        /// Is there a transaction open?
        /// On  a connection provider we do not manage so our response is always false
        /// </summary>
        public virtual bool HasOpenTransaction { get => false;  }

        /// <summary>
        /// Is there a shared connection? (Do we maintain state of just create anew)
        /// On  a connection provider we do not have shared connections so our response is always false
        /// </summary>
        public virtual bool IsSharedConnection { get => false; }

        /// <summary>
        /// Rolls back a transaction
        /// On a connection provider we do not manage transactions so our response is always false
        /// </summary>
        public virtual void Rollback()
        {
        }

        /// <summary>
        /// Rolls back a transaction
        /// On a connection provider we do not manage transactions so our response is always false
        /// </summary>
        public virtual Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        ~RelationalDbConnectionProvider() => Dispose(false);
        
        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    /* No shared transactions, nothing to do */
                }
                _disposed = true;
            }
        }
    }
}
