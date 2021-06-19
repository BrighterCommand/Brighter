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
            IsScoped = configuration.IsScoped;
        }

        public bool IsScoped { get; }

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
