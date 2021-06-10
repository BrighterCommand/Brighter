using System.Data.Common;
using System.Data.SqlClient;
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

        public DbConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<DbConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

            tcs.SetResult(GetConnection());

            return await tcs.Task;
        }
    }
}
