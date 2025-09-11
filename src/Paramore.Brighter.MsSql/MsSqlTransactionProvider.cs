using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.MsSql
{
    /// <summary>
    /// A connection provider for Sqlite 
    /// </summary>
    public class MsSqlTransactionProvider : RelationalDbTransactionProvider
    {
        private readonly string _connectionString;
 
        /// <summary>
        /// Create a connection provider for MSSQL using a connection string for Db access
        /// </summary>
        /// <param name="configuration">The configuration for this database</param>
        public MsSqlTransactionProvider(IAmARelationalDatabaseConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration?.ConnectionString))
                throw new ArgumentNullException(nameof(configuration.ConnectionString));
            _connectionString = configuration!.ConnectionString!;
        }

        /// <summary>
        /// Create a new Sql Connection and open it
        /// </summary>
        /// <returns></returns>
        public override DbConnection GetConnection()
        {
            if (Connection == null) Connection = new SqlConnection(_connectionString);
            if (Connection.State != System.Data.ConnectionState.Open) Connection.Open();
            return Connection;
        }

        /// <summary>
        /// Create a new Sql Connection and open it
        /// </summary>
        /// <returns></returns>
        public override async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (Connection == null) Connection = new SqlConnection(_connectionString);
            if (Connection.State != System.Data.ConnectionState.Open) await Connection.OpenAsync(cancellationToken);
            return Connection;
        }

        /// <summary>
        /// Creates and opens a Sql Transaction
        /// This is a shared transaction and you should manage it through the unit of work
        /// </summary>
        /// <returns>A shared transaction</returns>
         public override DbTransaction GetTransaction()
        {
            if (Connection == null) Connection = GetConnection();
            if (!HasOpenTransaction)
                Transaction = ((SqlConnection) Connection).BeginTransaction();
            return Transaction!;
        }

        /// <summary>
        /// Creates and opens a Sql Transaction
        /// This is a shared transaction and you should manage it through the unit of work
        /// </summary>
        /// <returns>A shared transaction</returns>
        public override async Task<DbTransaction> GetTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (Connection == null) Connection = await GetConnectionAsync(cancellationToken);
            if (!HasOpenTransaction)
                Transaction = ((SqlConnection) Connection).BeginTransaction();
            return Transaction!;
        }
    }
}
