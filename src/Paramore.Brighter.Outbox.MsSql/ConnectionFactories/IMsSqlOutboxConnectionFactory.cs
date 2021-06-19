using Microsoft.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Outbox.MsSql.ConnectionFactories
{
    public interface IMsSqlOutboxConnectionFactory
    {
        bool IsScoped { get; }
        SqlConnection GetConnection();
        Task<SqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}
