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
    public class PostgreSqlConnectionProvider : RelationalDbConnectionProvider
    {
        private NpgsqlDataSource? _dataSource;
        private readonly string _connectionString;

        /// <summary>
        /// Initialise a new instance of PostgreSQl Connection provider from a connection string
        /// </summary>
        /// <param name="configuration">PostgreSQL Configuration</param>
        /// <param name="dataSource">From v7.0 Npgsql uses an Npgsql data source, leave null to have Brighter manage
        /// connections; Brighter will not manage type mapping for you in this case so you must register them
        /// globally</param>
        public PostgreSqlConnectionProvider(
            IAmARelationalDatabaseConfiguration configuration,
            NpgsqlDataSource? dataSource = null)
        {
            if (string.IsNullOrWhiteSpace(configuration?.ConnectionString))
                throw new ArgumentNullException(nameof(configuration.ConnectionString));

            _connectionString = configuration.ConnectionString;
            _dataSource = dataSource;
        }

        /// <summary>
        /// Close any open Npgsql data source
        /// </summary>
        public override void Close()
        {
            if (HasDataSource()) _dataSource?.Dispose();
            _dataSource = null;
            base.Close();
        }

        /// <summary>
        /// Gets a existing Connection; creates a new one if it does not exist
        /// The connection is not opened, you need to open it yourself.
        /// </summary>
        /// <returns>A database connection</returns>
        public override DbConnection GetConnection()
        {
            if (HasDataSource())
            {
                return _dataSource!.OpenConnection();
            }
            
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
            var connection = HasDataSource() ? _dataSource!.CreateConnection() : new NpgsqlConnection(_connectionString);
            if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
            return connection;
        }
        
        protected override void Dispose(bool disposing)
        {
            if (HasDataSource()) _dataSource!.Dispose();
            _dataSource = null; 
            base.Dispose(disposing);
        }

        private bool HasDataSource()
        {
            return _dataSource != null;
        }

    }
}
