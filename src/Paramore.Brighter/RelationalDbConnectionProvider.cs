using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    public abstract class RelationalDbConnectionProvider : IAmARelationalDbConnectionProvider
    {
        private bool _disposed = false;
        protected DbConnection Connection;
        protected DbTransaction Transaction;
        
        
        /// <summary>
        /// Get a database connection from the underlying provider
        /// </summary>
        /// <returns></returns>
        public abstract DbConnection GetConnection();

        /// <summary>
        /// Get a database connection from the underlying provider
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<DbConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            tcs.SetResult(GetConnection());
            return await tcs.Task;
        }

        /// <summary>
        /// Returns a transaction against the underlying database
        /// </summary>
        /// <returns></returns>
        public virtual DbTransaction GetTransaction()
        {
            if (!HasOpenTransaction)
                Transaction = GetConnection().BeginTransaction();
            return Transaction;
        }
        
        /// <summary>
        /// Returns a transaction against the underlying database
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual Task<DbTransaction> GetTransactionAsync(CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<DbTransaction>();
            
            if(cancellationToken.IsCancellationRequested)
                tcs.SetCanceled();
            
            tcs.SetResult(GetTransaction());
            return tcs.Task;
        }

        /// <summary>
        /// Is there already a transaction open against the underlying database
        /// </summary>
        public virtual bool HasOpenTransaction { get { return Transaction != null; } }

        /// <summary>
        /// Does the underlying provider share connections 
        /// </summary>
        public virtual bool IsSharedConnection { get => true; }
        
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
                    Connection?.Dispose();
                    Transaction?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
