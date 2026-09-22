using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Paramore.Brighter.MsSql.EntityFrameworkCore
{
    public class MsSqlEntityFrameworkCoreTransactionProvider<T> : RelationalDbTransactionProvider where T : DbContext
    {
        private readonly T _context;
        
        /// <summary>
        /// Initialise a new instance of Ms Sql Connection provider using the Database Connection from an Entity Framework Core DbContext.
        /// </summary>
        public MsSqlEntityFrameworkCoreTransactionProvider(T context)
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
        /// Gets a existing Connection; creates a new one if it does not exist
        /// Opens the connection if it is not opened 
        /// </summary>
        /// <returns>A database connection</returns>
        public override DbConnection GetConnection()
        {
            //This line ensure that the connection has been initialised and that any required interceptors have been run before getting the connection
            _context.Database.CanConnect();
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open) connection.Open();
            return connection;
        }

        /// <summary>
        /// Gets a existing Connection; creates a new one if it does not exist
        /// The connection is not opened, you need to open it yourself.
        /// The base class just returns a new or existing connection, but derived types may perform async i/o
        /// </summary>
        /// <returns>A database connection</returns>
        public override async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            //This line ensure that the connection has been initialised and that any required interceptors have been run before getting the connection
            await _context.Database.CanConnectAsync(cancellationToken);
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
            return connection;
        }

        /// <summary>
        /// Gets an existing transaction; creates a new one from the connection if it does not exist.
        /// You should use the commit transaction using the Commit method. 
        /// </summary>
        /// <returns>A database transaction</returns>
        public override DbTransaction GetTransaction()
        {
            var currentTransaction = _context.Database.CurrentTransaction;
            if (currentTransaction is null)
            {
                // If there is no current transaction, we create a new one
                _context.Database.BeginTransaction();
                currentTransaction = _context.Database.CurrentTransaction;
            }

            return currentTransaction!.GetDbTransaction();
        }

        /// <summary>
        /// Rolls back a transaction
        /// </summary>
        public override async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (HasOpenTransaction)
            {
                try { await ((SqlTransaction)GetTransaction()).RollbackAsync(cancellationToken); } catch (Exception) { /* Ignore*/}
                Transaction = null;
            }
        }


        public override bool HasOpenTransaction => _context.Database.CurrentTransaction != null;

        public override bool IsSharedConnection => true;
    }
}
