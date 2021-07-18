using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.MsSql
{
    public interface IMsSqlConnectionProvider : IAmABoxConnectionProvider
    {
        SqlConnection GetConnection();
        Task<SqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default(CancellationToken));
        SqlTransaction GetTransaction();
        bool HasOpenTransaction { get; }
        bool IsSharedConnection { get; }
    }
}
