using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.MsSql
{
    public class MsSqlSqlAuthConnectionProvider : IMsSqlConnectionProvider
    {
        private readonly string _connectionString;

        /// <summary>
        /// Initialise a new instance of Ms Sql Connection provider using Sql Authentication.
        /// </summary>
        /// <param name="configuration">Ms Sql Configuration</param>
        public MsSqlSqlAuthConnectionProvider(MsSqlConfiguration configuration)
        {
            _connectionString = configuration.ConnectionString;
        }

        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public async Task<SqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<SqlConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

            tcs.SetResult(GetConnection());
            return await tcs.Task;
        }

        public SqlTransaction GetTransaction()
        {
            //This Connection Factory does not support Transactions
            return null;
        }

        public bool HasOpenTransaction { get => false; }
        public bool IsSharedConnection { get => false; }
    }
}
