using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MySqlConnector;

namespace Paramore.Brighter.MySql.EntityFrameworkCore
{
    /// <summary>
    /// A connection provider that uses the same connection as EF Core
    /// </summary>
    /// <typeparam name="T">The Db Context to take the connection from</typeparam>
    public class MySqlEntityFrameworkTransactionProvider<T> : RelationalDbTransactionProvider where T: DbContext
    {
        private readonly T _context;

        /// <summary>
        /// Constructs and instance from a Db context
        /// </summary>
        /// <param name="context">The database context to use</param>
        public MySqlEntityFrameworkTransactionProvider(T context)
        {
            _context = context;
        }
        
        /// <summary>
        /// Commit the transaction
        /// </summary>
        public override void Commit()
        {
            if (HasOpenTransaction)
            {
                _context.Database.CurrentTransaction?.Commit();
            }
        }
        
        /// <summary>
        /// Commit the transaction
        /// </summary>
        /// <returns>An awaitable Task</returns>
        public override Task CommitAsync(CancellationToken cancellationToken)
        {
            if (HasOpenTransaction)
            {
                _context.Database.CurrentTransaction?.CommitAsync(cancellationToken);
            }
            
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Get the current connection of the DB context
        /// </summary>
        /// <returns>The Sqlite Connection that is in use</returns>
        public override DbConnection GetConnection()
        {
            //This line ensure that the connection has been initialised and that any required interceptors have been run before getting the connection
            _context.Database.CanConnect();
            return _context.Database.GetDbConnection();
        }

        /// <summary>
        /// Get the current connection of the DB context
        /// </summary>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns></returns>
        public override async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            //This line ensure that the connection has been initialised and that any required interceptors have been run before getting the connection
            await _context.Database.CanConnectAsync(cancellationToken);
            return _context.Database.GetDbConnection();
        }

        /// <summary>
        /// Get the ambient EF Core Transaction
        /// </summary>
        /// <returns>The Sqlite Transaction</returns>
        public override DbTransaction GetTransaction()
        {
            var currentTransaction = _context.Database.CurrentTransaction;
            if (currentTransaction == null)
            {
                // If there is no current transaction, we create a new one
                currentTransaction = _context.Database.BeginTransaction();
            }
            return currentTransaction.GetDbTransaction();
        }
        
        /// <summary>
        /// Rolls back a transaction
        /// </summary>
        public override async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (HasOpenTransaction)
            {
                try { await ((MySqlTransaction)GetTransaction()).RollbackAsync(cancellationToken); } catch (Exception) { /* Ignore*/}
                Transaction = null;
            }
        }

        public override bool HasOpenTransaction => _context.Database.CurrentTransaction != null;
        public override bool IsSharedConnection => true;
    }
}
