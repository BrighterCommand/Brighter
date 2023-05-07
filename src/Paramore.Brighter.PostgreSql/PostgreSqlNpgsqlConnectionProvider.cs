using System;
using System.Data.Common;
using Npgsql;

namespace Paramore.Brighter.PostgreSql
{
    public class PostgreSqlNpgsqlConnectionProvider : RelationalDbConnectionProvider
    {
        private readonly string _connectionString;

        public PostgreSqlNpgsqlConnectionProvider(RelationalDatabaseConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration?.ConnectionString))
                throw new ArgumentNullException(nameof(configuration.ConnectionString));

            _connectionString = configuration.ConnectionString;
        }

        public override DbConnection GetConnection()
        
        {
            return new NpgsqlConnection(_connectionString);
        }
    }
}
