using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Paramore.Brighter.PostgreSql
{
    /// <summary>
    /// A connection provider that uses the connection string to create a connection
    /// </summary>
    public class PostgreSqlNpgsqlConnectionProvider : RelationalDbConnectionProvider
    {
        private readonly string _connectionString;

        /// <summary>
        /// Initialise a new instance of PostgreSQl Connection provider from a connection string
        /// </summary>
        /// <param name="configuration">PostgreSQL Configuration</param>
        public PostgreSqlNpgsqlConnectionProvider(IAmARelationalDatabaseConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration?.ConnectionString))
                throw new ArgumentNullException(nameof(configuration.ConnectionString));

            _connectionString = configuration.ConnectionString;
        }

        /// <summary>
        /// Gets a existing Connection; creates a new one if it does not exist
        /// The connection is not opened, you need to open it yourself.
        /// </summary>
        /// <returns>A database connection</returns>
        public override DbConnection GetConnection()
        {
            var connection = new NpgsqlConnection(_connectionString);
            if (connection.State != System.Data.ConnectionState.Open) connection.Open();
            return connection;
        }

        /// <summary>
        /// Gets a existing Connection; creates a new one if it does not exist
        /// The connection is not opened, you need to open it yourself.
        /// </summary>
        /// <returns>A database connection</returns>
         public override async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = new NpgsqlConnection(_connectionString);
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }
}
