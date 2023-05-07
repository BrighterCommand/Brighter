using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.MsSql
{
    public class MsSqlSqlAuthConnectionProvider : RelationalDbConnectionProvider
    {
        private readonly string _connectionString;

        /// <summary>
        /// Initialise a new instance of Ms Sql Connection provider using Sql Authentication.
        /// </summary>
        /// <param name="configuration">Ms Sql Configuration</param>
        public MsSqlSqlAuthConnectionProvider(RelationalDatabaseConfiguration configuration)
        {
            _connectionString = configuration.ConnectionString;
        }

        public override DbConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
