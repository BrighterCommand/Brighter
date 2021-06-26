using Microsoft.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.MsSql.ConnectionFactories
{
    public class MsSqlMessagingGatewaySqlAuthConnectionFactory : IMsSqlMessagingGatewayConnectionFactory
    {
        private readonly string _connectionString;

        public MsSqlMessagingGatewaySqlAuthConnectionFactory(string connectionString)
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
