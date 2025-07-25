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
    public class MsSqlConnectionProvider : RelationalDbConnectionProvider
    {
        private readonly string _connectionString;
 
        /// <summary>
        /// Create a connection provider for MSSQL using a connection string for Db access
        /// </summary>
        /// <param name="configuration">The configuration for this database</param>
        public MsSqlConnectionProvider(IAmARelationalDatabaseConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration?.ConnectionString))
                throw new ArgumentNullException(nameof(configuration.ConnectionString));
            _connectionString = configuration!.ConnectionString!;
        }

        /// <summary>
        /// Create a new Sql Connection and open it
        /// This is not a shared connection and you should manage it's lifetime
        /// </summary>
        /// <returns></returns>
        public override DbConnection GetConnection()
        {
            var connection = new SqlConnection(_connectionString);
            if (connection.State != System.Data.ConnectionState.Open) connection.Open();
            return connection;
        }

        /// <summary>
        /// Create a new Sql Connection and open it
        /// This is not a shared connection and you should manage it's lifetime
        /// </summary>
        /// <returns></returns>
         public override async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = new SqlConnection(_connectionString);
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }
}
