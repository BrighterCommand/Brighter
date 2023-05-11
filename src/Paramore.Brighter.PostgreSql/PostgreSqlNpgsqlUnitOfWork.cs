using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Paramore.Brighter.PostgreSql
{
    /// <summary>
    /// A connection provider that uses the connection string to create a connection
    /// </summary>
    public class PostgreSqlNpgsqlUnitOfWork : RelationalDbTransactionProvider
    {
        private readonly string _connectionString;

        /// <summary>
        /// Initialise a new instance of PostgreSQl Connection provider from a connection string
        /// </summary>
        /// <param name="configuration">PostgreSQL Configuration</param>
        public PostgreSqlNpgsqlUnitOfWork(IAmARelationalDatabaseConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration?.ConnectionString))
                throw new ArgumentNullException(nameof(configuration.ConnectionString));

            _connectionString = configuration.ConnectionString;
        }
        
        /// <summary>
        /// Commit the transaction
        /// </summary>
        /// <returns>An awaitable Task</returns>
        public override Task CommitAsync(CancellationToken cancellationToken)
        {
            if (HasOpenTransaction)
            {
                ((NpgsqlTransaction)Transaction).CommitAsync(cancellationToken);
                Transaction = null;
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets a existing Connection; creates a new one if it does not exist
        /// This is a shared connection and you should manage it via this interface 
        /// </summary>
        /// <returns>A database connection</returns>
        public override DbConnection GetConnection()
        {
            if (Connection == null) Connection = new NpgsqlConnection(_connectionString);
            if (Connection.State != ConnectionState.Open) Connection.Open();
            return Connection;
        }

        /// <summary>
        /// Gets a existing Connection; creates a new one if it does not exist
        /// This is a shared connection and you should manage it via this interface 
        /// </summary>
        /// <returns>A database connection</returns>
        public override async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (Connection == null) Connection = new NpgsqlConnection(_connectionString);
            if (Connection.State != ConnectionState.Open) await Connection.OpenAsync(cancellationToken);
            return Connection;
        }

        /// <summary>
        /// Either returns an existing open transaction or creates and opens a MySql Transaction
        /// This is a shared transaction and you should manage it through the unit of work
        /// </summary>
        /// <returns></returns>
        public override DbTransaction GetTransaction()
        {
            if (Connection == null) Connection = GetConnection();
            if (Connection.State != ConnectionState.Open)
                Connection.Open();
            if (!HasOpenTransaction)
                Transaction = ((NpgsqlConnection)Connection).BeginTransaction();
            return Transaction;
        }

        /// <summary>
        /// Creates and opens a MySql Transaction
        /// This is a shared transaction and you should manage it through the unit of work
        /// </summary>
        /// <returns></returns>
        public override async Task<DbTransaction> GetTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (Connection == null) Connection = await GetConnectionAsync(cancellationToken);
            if (Connection.State != ConnectionState.Open)
                await Connection.OpenAsync(cancellationToken);
            if (!HasOpenTransaction)
                Transaction = ((NpgsqlConnection)Connection).BeginTransaction();
            return Transaction;
        }
    }
}
