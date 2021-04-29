using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Outbox.MsSql.ConnectionFactories
{
    public interface IMsSqlOutboxConnectionFactory
    {
        DbConnection GetConnection();
        Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}
