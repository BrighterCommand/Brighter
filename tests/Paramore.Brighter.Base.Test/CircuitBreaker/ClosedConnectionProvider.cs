using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Base.Test.CircuitBreaker;

public class ClosedConnectionProvider(IAmARelationalDbConnectionProvider inner) : IAmARelationalDbConnectionProvider
{
    public DbConnection GetConnection()
    {
        var connection = inner.GetConnection();
        connection.Close();
        return connection;
    }

    public async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = await inner.GetConnectionAsync(cancellationToken);
        await connection.CloseAsync();
        return connection;
    }
}
