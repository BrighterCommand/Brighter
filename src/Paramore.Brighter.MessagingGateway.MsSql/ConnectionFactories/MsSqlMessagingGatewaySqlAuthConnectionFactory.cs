using System.Data.Common;
using System.Data.SqlClient;
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

        public DbConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return GetConnection();
        }
    }
}
