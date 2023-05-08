using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    public interface IAmARelationalDbConnectionProvider
    {
        DbConnection GetConnection();

        Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default);

        DbTransaction GetTransaction();
        
        Task<DbTransaction> GetTransactionAsync(CancellationToken cancellationToken = default);

        bool HasOpenTransaction { get; }

        bool IsSharedConnection { get; }
    }
}
