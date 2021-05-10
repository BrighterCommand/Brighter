using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Inbox.MsSql.ConnectionFactories
{
    public interface IMsSqlInboxConnectionFactory
    {
        DbConnection GetConnection();
        Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}
