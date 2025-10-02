using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    public abstract class RelationalDbTransactionProvider : IAmATransactionConnectionProvider
    {
        private bool _disposed = false;
        protected DbConnection? Connection;
        
        protected DbTransaction? Transaction { get; set; }
        
        /// <summary>
        /// Close any open connection or transaction
        /// </summary>
        public virtual void Close()
        {
            if (!HasOpenTransaction)
            {
                Transaction?.Dispose();
                Transaction = null;
            }

            if (!IsSharedConnection)
                Connection?.Close();
        }
        
        /// <summary>
        /// Commit the transaction
        /// </summary>
        public virtual void Commit()
        {
            if (HasOpenTransaction)
            {
                Transaction!.Commit();
                Transaction = null;
            }
        }

        /// <summary>
        /// Commit the transaction
        /// </summary>
        /// <returns>An awaitable Task</returns>
        public virtual Task CommitAsync(CancellationToken cancellationToken)
        {
            if (HasOpenTransaction)
            {
                Transaction!.Commit();
                Transaction = null;
            }
            
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Gets a existing Connection; creates a new one if it does not exist
        /// Opens the connection if it is not opened 
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
        /// Gets an existing transaction; creates a new one from the connection if it does not exist.
        /// You should use the commit transaction using the Commit method. 
        /// </summary>
        /// <returns>A database transaction</returns>
        public virtual DbTransaction GetTransaction()
        {
            Connection ??= GetConnection();
            if (Connection.State != ConnectionState.Open)
                Connection.Open();
            if (!HasOpenTransaction)
                Transaction = Connection.BeginTransaction();
            return Transaction!;
        }
        
        /// <summary>
        /// Gets an existing transaction; creates a new one from the connection if it does not exist.
        /// You are responsible for committing the transaction.
        /// </summary>
        /// <returns>A database transaction</returns>
        public virtual Task<DbTransaction> GetTransactionAsync(CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<DbTransaction>();
            
            if(cancellationToken.IsCancellationRequested)
                tcs.SetCanceled();
            
            tcs.SetResult(GetTransaction());
            return tcs.Task;
        }

        /// <summary>
        /// Is there a transaction open?
        /// </summary>
#if !NETSTANDARD
        [MemberNotNullWhen(true,  nameof(Transaction))]
#endif
        public virtual bool HasOpenTransaction  => Transaction != null;

        /// <summary>
        /// Is there a shared connection? (Do we maintain state of just create anew)
        /// </summary>
        public virtual bool IsSharedConnection => true;
        
        /// <summary>
        /// Rolls back a transaction
        /// </summary>
        public virtual void Rollback()
        {
            if (HasOpenTransaction)
            {
                try { Transaction!.Rollback(); } catch(Exception) { /*ignore*/ }
                Transaction = null;
            }
        }

        /// <summary>
        /// Rolls back a transaction
        /// </summary>
        public virtual Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (HasOpenTransaction)
            {
                try { Transaction!.Rollback(); } catch(Exception) { /*ignore*/ }
                Transaction = null;
            }
            
            return Task.CompletedTask;
        }
        
        ~RelationalDbTransactionProvider() => Dispose(false);
        
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
                Connection = null;
                Transaction = null;
                _disposed = true;
            }
        }
    }
}
