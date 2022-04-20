using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Paramore.Brighter.PostgreSql
{
    public interface IPostgreSqlConnectionProvider
    {
        NpgsqlConnection GetConnection();

        Task<NpgsqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default);

        NpgsqlTransaction GetTransaction();

        bool HasOpenTransaction { get; }

        bool IsSharedConnection { get; }
    }
}
