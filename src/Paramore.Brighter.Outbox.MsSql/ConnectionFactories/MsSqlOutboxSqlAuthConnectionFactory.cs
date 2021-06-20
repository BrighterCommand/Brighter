using Microsoft.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Outbox.MsSql.ConnectionFactories
{
    public class MsSqlOutboxSqlAuthConnectionFactory : IMsSqlOutboxConnectionFactory
    {
        private readonly string _connectionString;

        public MsSqlOutboxSqlAuthConnectionFactory(MsSqlOutboxConfiguration configuration)
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
