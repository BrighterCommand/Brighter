using Microsoft.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Inbox.MsSql.ConnectionFactories
{
    public class MsSqlInboxSqlAuthConnectionFactory : IMsSqlInboxConnectionFactory
    {
        private readonly string _connectionString;

        public MsSqlInboxSqlAuthConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
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
    }
}
