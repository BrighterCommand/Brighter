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
    public class MySqlEntityFrameworkConnectionProvider<T> : IMySqlTransactionConnectionProvider where T: DbContext
    {
        private readonly T _context;

        /// <summary>
        /// Constructs and instance from a Db context
        /// </summary>
        /// <param name="context">The database context to use</param>
        public MySqlEntityFrameworkConnectionProvider(T context)
        {
            _context = context;
        }
        
        /// <summary>
        /// Get the current connection of the DB context
        /// </summary>
        /// <returns>The Sqlite Connection that is in use</returns>
        public MySqlConnection GetConnection()
        {
            //This line ensure that the connection has been initialised and that any required interceptors have been run before getting the connection
            _context.Database.CanConnect();
            return (MySqlConnection) _context.Database.GetDbConnection();
        }

        /// <summary>
        /// Get the current connection of the DB context
        /// </summary>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns></returns>
        public async Task<MySqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            //This line ensure that the connection has been initialised and that any required interceptors have been run before getting the connection
            await _context.Database.CanConnectAsync(cancellationToken);
            return (MySqlConnection)_context.Database.GetDbConnection();
        }

        /// <summary>
        /// Get the ambient EF Core Transaction
        /// </summary>
        /// <returns>The Sqlite Transaction</returns>
        public MySqlTransaction GetTransaction()
        {
            return (MySqlTransaction)_context.Database.CurrentTransaction?.GetDbTransaction();
        }

        public bool HasOpenTransaction { get => _context.Database.CurrentTransaction != null; }
        public bool IsSharedConnection { get => true; }
    }
}
