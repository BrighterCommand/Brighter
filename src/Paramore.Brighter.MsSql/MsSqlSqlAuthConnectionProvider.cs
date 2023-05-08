using System;
using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.MsSql
{
    /// <summary>
    /// A connection provider for Sqlite 
    /// </summary>
    public class MsSqlSqlAuthConnectionProvider : RelationalDbConnectionProvider, IAmATransactionConnectionProvider
    {
        private readonly string _connectionString;
 
        /// <summary>
        /// Create a connection provider for MSSQL using a connection string for Db access
        /// </summary>
        /// <param name="configuration">The configuration for this database</param>
        public MsSqlSqlAuthConnectionProvider(IAmARelationalDatabaseConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration?.ConnectionString))
                throw new ArgumentNullException(nameof(configuration.ConnectionString));
            _connectionString = configuration.ConnectionString;
        }

        public override DbConnection GetConnection() =>  Connection ?? (Connection = new SqlConnection(_connectionString));
    }
}
