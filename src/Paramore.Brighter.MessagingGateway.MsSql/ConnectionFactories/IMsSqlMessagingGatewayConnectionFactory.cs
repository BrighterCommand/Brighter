using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.MessagingGateway.MsSql.ConnectionFactories
{
    public interface IMsSqlMessagingGatewayConnectionFactory
    {
        SqlConnection GetConnection();
        Task<SqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}
