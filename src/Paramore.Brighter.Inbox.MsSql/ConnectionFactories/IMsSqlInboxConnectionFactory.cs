using Microsoft.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Inbox.MsSql.ConnectionFactories
{
    public interface IMsSqlInboxConnectionFactory
    {
        SqlConnection GetConnection();
        Task<SqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}
