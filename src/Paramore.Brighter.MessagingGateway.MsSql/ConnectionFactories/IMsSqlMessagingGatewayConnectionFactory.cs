using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.MsSql.ConnectionFactories
{
    public interface IMsSqlMessagingGatewayConnectionFactory
    {
        DbConnection GetConnection();
        Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}
